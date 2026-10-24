using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Complot.Domain.Planner;
using Microsoft.Extensions.Caching.Memory;

namespace Complot.Api.Features.Planner;

/// <summary>OAuth client of a Google Cloud project (type "Web application"); empty = no Google Calendar link.</summary>
public sealed class GoogleOptions
{
    public const string Section = "Google";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>Optional fixed redirect URI; by default it is derived from the request (…/api/planner/google/callback).</summary>
    public string? RedirectUri { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed record GoogleAccount(string RefreshToken, string Email);

/// <summary>Busy time in someone's calendar, UTC.</summary>
public sealed record BusyInterval(DateTime Start, DateTime End);

/// <summary>A Google API call failed; <see cref="Code"/> is an error code for the UI ("calendar.reconnect" when access was revoked).</summary>
public sealed class GoogleCalendarException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
}

/// <summary>The few Google calls the planner needs: link an account, read busy times, write events.</summary>
public interface IGoogleCalendar
{
    Uri AuthorizeUrl(string state, Uri redirectUri);
    Task<GoogleAccount> ExchangeCodeAsync(string code, Uri redirectUri, CancellationToken ct);
    Task<IReadOnlyList<BusyInterval>> BusyAsync(string refreshToken, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>Updates the event when <paramref name="eventId"/> still exists, otherwise creates one; returns its id.</summary>
    Task<string> SaveEventAsync(string refreshToken, string? eventId, CalendarEntry entry, string timeZone, CancellationToken ct);

    Task DeleteEventAsync(string refreshToken, string eventId, CancellationToken ct);
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}

/// <summary>Google OAuth 2.0 and Calendar v3 over plain HTTP (no SDK needed for these calls).</summary>
public sealed class GoogleCalendarClient(HttpClient http, GoogleOptions options, IMemoryCache cache) : IGoogleCalendar
{
    /// <summary>Write events, read free/busy, and the e-mail address of the account (to show which calendar is linked).</summary>
    private const string Scopes = "openid email https://www.googleapis.com/auth/calendar.events https://www.googleapis.com/auth/calendar.freebusy";
    private const string CalendarApi = "https://www.googleapis.com/calendar/v3";

    public Uri AuthorizeUrl(string state, Uri redirectUri) => new(QueryHelpers("https://accounts.google.com/o/oauth2/v2/auth", new()
    {
        ["client_id"] = options.ClientId!,
        ["redirect_uri"] = redirectUri.ToString(),
        ["response_type"] = "code",
        ["scope"] = Scopes,
        ["access_type"] = "offline",
        ["prompt"] = "consent",
        ["include_granted_scopes"] = "true",
        ["state"] = state,
    }));

    public async Task<GoogleAccount> ExchangeCodeAsync(string code, Uri redirectUri, CancellationToken ct)
    {
        var token = await TokenRequestAsync(new()
        {
            ["code"] = code,
            ["client_id"] = options.ClientId!,
            ["client_secret"] = options.ClientSecret!,
            ["redirect_uri"] = redirectUri.ToString(),
            ["grant_type"] = "authorization_code",
        }, ct);
        var refresh = token["refresh_token"]?.GetValue<string>() ?? throw new GoogleCalendarException("calendar.failed", "Google returned no refresh token.");
        var email = EmailFromIdToken(token["id_token"]?.GetValue<string>()) ?? "?";
        return new GoogleAccount(refresh, email);
    }

    public async Task<IReadOnlyList<BusyInterval>> BusyAsync(string refreshToken, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var body = new { timeMin = Rfc3339(fromUtc), timeMax = Rfc3339(toUtc), items = new[] { new { id = "primary" } } };
        var result = await SendAsync(refreshToken, HttpMethod.Post, $"{CalendarApi}/freeBusy", JsonContent.Create(body), ct);
        return result?["calendars"]?["primary"]?["busy"]?.AsArray()
            .Select(b => new BusyInterval(ParseUtc(b!["start"]!), ParseUtc(b!["end"]!)))
            .ToList() ?? [];
    }

    public async Task<string> SaveEventAsync(string refreshToken, string? eventId, CalendarEntry entry, string timeZone, CancellationToken ct)
    {
        var body = new
        {
            summary = entry.Title,
            location = entry.Location,
            description = entry.Description,
            start = new { dateTime = Local(entry.Start), timeZone },
            end = new { dateTime = Local(entry.End), timeZone },
            // Garden work should not block other appointments in free/busy (and not show up as busy in the planner).
            transparency = "transparent",
            extendedProperties = new { @private = new { complotKey = entry.Uid } },
        };
        if (eventId is not null)
        {
            try
            {
                var patched = await SendAsync(refreshToken, HttpMethod.Patch, $"{CalendarApi}/calendars/primary/events/{Uri.EscapeDataString(eventId)}", JsonContent.Create(body), ct);
                if (patched?["status"]?.GetValue<string>() != "cancelled") return eventId;
            }
            catch (GoogleCalendarException e) when (e.InnerException is HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Gone })
            {
                // Deleted in Google: write it again.
            }
        }
        var created = await SendAsync(refreshToken, HttpMethod.Post, $"{CalendarApi}/calendars/primary/events", JsonContent.Create(body), ct);
        return created?["id"]?.GetValue<string>() ?? throw new GoogleCalendarException("calendar.failed", "Google returned no event id.");
    }

    public async Task DeleteEventAsync(string refreshToken, string eventId, CancellationToken ct)
    {
        try
        {
            await SendAsync(refreshToken, HttpMethod.Delete, $"{CalendarApi}/calendars/primary/events/{Uri.EscapeDataString(eventId)}", null, ct);
        }
        catch (GoogleCalendarException e) when (e.InnerException is HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Gone })
        {
            // Already gone.
        }
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        using var response = await http.PostAsync("https://oauth2.googleapis.com/revoke",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refreshToken }), ct);
        cache.Remove(CacheKey(refreshToken));
    }

    private async Task<JsonNode?> SendAsync(string refreshToken, HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync(refreshToken, ct));
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized) cache.Remove(CacheKey(refreshToken));
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            throw new GoogleCalendarException("calendar.failed", $"Google Calendar {method} failed: {(int)response.StatusCode}", e);
        }
        return response.Content.Headers.ContentLength == 0 || response.StatusCode == HttpStatusCode.NoContent
            ? null
            : await response.Content.ReadFromJsonAsync<JsonNode>(ct);
    }

    /// <summary>Access tokens live about an hour; they are cached per refresh token.</summary>
    private async Task<string> AccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey(refreshToken), out string? cached) && cached is not null) return cached;
        var token = await TokenRequestAsync(new()
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = options.ClientId!,
            ["client_secret"] = options.ClientSecret!,
            ["grant_type"] = "refresh_token",
        }, ct);
        var access = token["access_token"]!.GetValue<string>();
        var lifetime = TimeSpan.FromSeconds((token["expires_in"]?.GetValue<int>() ?? 3600) - 120);
        cache.Set(CacheKey(refreshToken), access, lifetime);
        return access;
    }

    private async Task<JsonNode> TokenRequestAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadFromJsonAsync<JsonNode>(ct);
        if (response.IsSuccessStatusCode && body is not null) return body;
        // invalid_grant: the person revoked access or the token expired (apps in "testing" mode: after 7 days).
        var code = body?["error"]?.GetValue<string>() == "invalid_grant" ? "calendar.reconnect" : "calendar.failed";
        throw new GoogleCalendarException(code, $"Google token request failed: {(int)response.StatusCode} {body?["error"]}");
    }

    private static string CacheKey(string refreshToken) => "google-access:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    /// <summary>The ID token comes straight from Google's token endpoint over TLS, so its payload is read without verifying.</summary>
    private static string? EmailFromIdToken(string? idToken)
    {
        if (idToken?.Split('.') is not [_, var payload, ..]) return null;
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/').PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')));
        return JsonNode.Parse(json)?["email"]?.GetValue<string>();
    }

    private static string QueryHelpers(string url, Dictionary<string, string> query) =>
        url + "?" + string.Join('&', query.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

    private static string Rfc3339(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    private static string Local(DateTime local) => local.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
    private static DateTime ParseUtc(JsonNode value) =>
        DateTime.Parse(value.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
}

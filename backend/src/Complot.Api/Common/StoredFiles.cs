using Microsoft.AspNetCore.Http.HttpResults;

namespace Complot.Api.Common;

/// <summary>What may be uploaded for one purpose: content types (with the extension they are stored under) and a size limit.</summary>
public sealed record FileKind(IReadOnlyDictionary<string, string> Types, long MaxBytes, string TypeError, string SizeError)
{
    private static readonly Dictionary<string, string> Images = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["image/heic"] = ".heic",
    };

    public static readonly FileKind Photo = new(Images, 10 * 1024 * 1024, "photoType", "photoTooLarge");

    /// <summary>Invoices: a PDF or a photo of the paper.</summary>
    public static readonly FileKind Document = new(
        new Dictionary<string, string>(Images, StringComparer.OrdinalIgnoreCase) { ["application/pdf"] = ".pdf" },
        15 * 1024 * 1024, "attachmentType", "attachmentTooLarge");
}

/// <summary>Uploaded files on disk, named after the record they belong to ("{id}.pdf").</summary>
public static class StoredFiles
{
    /// <summary>A 400 for the <c>file</c> field when the upload is of the wrong type or too large; null when it is fine.</summary>
    public static ProblemHttpResult? Check(IFormFile file, FileKind kind)
    {
        ErrorDetail? error = !kind.Types.ContainsKey(file.ContentType) ? new ErrorDetail(kind.TypeError)
            : file.Length > kind.MaxBytes ? new ErrorDetail(kind.SizeError, new Dictionary<string, object?> { ["maxMb"] = kind.MaxBytes / 1024 / 1024 })
            : null;
        return error is null ? null : ApiProblems.Validation(new Dictionary<string, ErrorDetail[]> { ["file"] = [error] });
    }

    /// <summary>Stores the upload (replacing <paramref name="previous"/>) and returns its file name.</summary>
    public static async Task<string> SaveAsync(IFormFile file, FileKind kind, string directory, Guid id, string? previous, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        Delete(directory, previous);
        var name = $"{id:N}{kind.Types[file.ContentType]}";
        await using var target = File.Create(Path.Combine(directory, name));
        await file.CopyToAsync(target, ct);
        return name;
    }

    public static PhysicalFileHttpResult Serve(FileKind kind, string directory, string name, string? downloadName = null) =>
        TypedResults.PhysicalFile(Path.Combine(directory, name), kind.Types.First(t => t.Value == Path.GetExtension(name)).Key, downloadName);

    public static void Delete(string directory, string? name)
    {
        if (name is not null) File.Delete(Path.Combine(directory, name));
    }
}

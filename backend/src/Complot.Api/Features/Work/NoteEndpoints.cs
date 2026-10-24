using Complot.Api.Auth;
using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Api.Hosting;
using Complot.Domain.Access;
using Complot.Domain.Work;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Complot.Api.Features.Work;

public sealed record NoteDto(
    Guid Id, Guid SiteId, Guid? BedId, Guid? TreeId, Guid? PlantingId, Guid? VarietyId, Guid? PersonId,
    DateOnly Date, NoteTag Tag, string Text, decimal? HarvestQuantity, string? HarvestUnit, bool HasPhoto);

public sealed record NoteInput(
    Guid SiteId, Guid? BedId, Guid? TreeId, Guid? PlantingId, Guid? PersonId,
    DateOnly Date, NoteTag Tag, string Text, decimal? HarvestQuantity, string? HarvestUnit);

public sealed class NoteInputValidator : AbstractValidator<NoteInput>
{
    public NoteInputValidator(ComplotDbContext db)
    {
        RuleFor(x => x.SiteId).MustAsync((id, ct) => db.Sites.ExistsAsync(id, ct)).WithErrorCode("unknownSite");
        RuleFor(x => x.BedId)
            .MustAsync((x, id, ct) => db.Beds.AnyAsync(b => b.Id == id && b.SiteId == x.SiteId, ct)).WithErrorCode("unknownBed")
            .When(x => x.BedId is not null);
        RuleFor(x => x.TreeId)
            .MustAsync((x, id, ct) => db.Trees.AnyAsync(t => t.Id == id && t.SiteId == x.SiteId, ct)).WithErrorCode("unknownTree")
            .When(x => x.TreeId is not null);
        RuleFor(x => x.PlantingId)
            .MustAsync((x, id, ct) => db.Plantings.AnyAsync(p => p.Id == id && p.BedId == x.BedId, ct)).WithErrorCode("unknownPlanting")
            .When(x => x.PlantingId is not null);
        RuleFor(x => x.PersonId).MustAsync((id, ct) => db.People.ExistsAsync(id!.Value, ct)).WithErrorCode("unknownPerson").When(x => x.PersonId is not null);
        RuleFor(x => x.Tag).IsInEnum();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.HarvestQuantity).GreaterThan(0).When(x => x.HarvestQuantity is not null);
        RuleFor(x => x.HarvestUnit).NotEmpty().MaximumLength(20).When(x => x.HarvestQuantity is not null);
    }
}

public static class NoteEndpoints
{

    public static void MapNotes(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/notes").WithTags("Notes");
        var target = SiteResolvers.Body<NoteInput>(x => x.SiteId);
        group.MapGet("/", List).WithName("ListNotes");
        group.MapPost("/", Create).WithName("CreateNote").Validate<NoteInput>().RequireSite(SiteRole.Edit, target);
        group.MapPut("/{id:guid}", Update).WithName("UpdateNote").Validate<NoteInput>()
            .RequireSite(SiteRole.Edit, SiteResolvers.Note).RequireSite(SiteRole.Edit, target);
        group.MapDelete("/{id:guid}", Delete).WithName("DeleteNote").RequireSite(SiteRole.Edit, SiteResolvers.Note);
        // Same-site cookies protect this upload; the multipart antiforgery token is not used by the SPA.
        group.MapPut("/{id:guid}/photo", UploadPhoto).WithName("UploadNotePhoto").DisableAntiforgery()
            .RequireSite(SiteRole.Edit, SiteResolvers.Note);
        group.MapGet("/{id:guid}/photo", GetPhoto).WithName("GetNotePhoto").RequireSite(SiteRole.View, SiteResolvers.Note);
    }

    private static NoteDto ToDto(Note n, Guid? varietyId) => new(
        n.Id, n.SiteId, n.BedId, n.TreeId, n.PlantingId, varietyId, n.PersonId, n.Date, n.Tag, n.Text, n.HarvestQuantity, n.HarvestUnit, n.PhotoFileName is not null);

    private static async Task<NoteDto[]> List(
        Guid? siteId, Guid? bedId, Guid? treeId, Guid? varietyId, ComplotDbContext db, CurrentUser user, CancellationToken ct)
    {
        var visible = await db.VisibleSiteIdsAsync(await user.GetAsync(ct), ct);
        var query = from note in db.Notes.AsNoTracking().Where(n => visible.Contains(n.SiteId))
                    join planting in db.Plantings on note.PlantingId equals planting.Id into plantings
                    from planting in plantings.DefaultIfEmpty()
                    select new { note, VarietyId = (Guid?)planting.VarietyId };
        if (siteId is not null) query = query.Where(x => x.note.SiteId == siteId);
        if (bedId is not null) query = query.Where(x => x.note.BedId == bedId);
        if (treeId is not null) query = query.Where(x => x.note.TreeId == treeId);
        if (varietyId is not null) query = query.Where(x => x.VarietyId == varietyId);
        var rows = await query.OrderByDescending(x => x.note.Date).ThenByDescending(x => x.note.CreatedAt).ToListAsync(ct);
        return rows.Select(x => ToDto(x.note, x.VarietyId)).ToArray();
    }

    private static async Task<Created<NoteDto>> Create(NoteInput input, ComplotDbContext db, CancellationToken ct)
    {
        var note = new Note { Text = "" };
        Apply(input, note);
        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/notes/{note.Id}", ToDto(note, await VarietyOf(note, db, ct)));
    }

    private static async Task<Results<Ok<NoteDto>, ProblemHttpResult>> Update(Guid id, NoteInput input, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Notes.FindAsync([id], ct) is not { } note) return ApiProblems.NotFound();
        Apply(input, note);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(note, await VarietyOf(note, db, ct)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, IOptions<StorageOptions> storage, CancellationToken ct)
    {
        if (await db.Notes.FindAsync([id], ct) is not { } note) return ApiProblems.NotFound();
        StoredFiles.Delete(storage.Value.PhotoDir, note.PhotoFileName);
        db.Notes.Remove(note);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UploadPhoto(
        Guid id, IFormFile file, ComplotDbContext db, IOptions<StorageOptions> storage, CancellationToken ct)
    {
        if (await db.Notes.FindAsync([id], ct) is not { } note) return ApiProblems.NotFound();
        if (StoredFiles.Check(file, FileKind.Photo) is { } invalid) return invalid;
        note.PhotoFileName = await StoredFiles.SaveAsync(file, FileKind.Photo, storage.Value.PhotoDir, note.Id, note.PhotoFileName, ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<PhysicalFileHttpResult, ProblemHttpResult>> GetPhoto(Guid id, ComplotDbContext db, IOptions<StorageOptions> storage, CancellationToken ct)
    {
        if (await db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct) is not { PhotoFileName: { } file }) return ApiProblems.NotFound();
        return StoredFiles.Serve(FileKind.Photo, storage.Value.PhotoDir, file);
    }

    private static async Task<Guid?> VarietyOf(Note note, ComplotDbContext db, CancellationToken ct) =>
        note.PlantingId is { } id ? await db.Plantings.Where(p => p.Id == id).Select(p => (Guid?)p.VarietyId).FirstOrDefaultAsync(ct) : null;

    private static void Apply(NoteInput input, Note note)
    {
        note.SiteId = input.SiteId;
        note.BedId = input.BedId;
        note.TreeId = input.TreeId;
        note.PlantingId = input.PlantingId;
        note.PersonId = input.PersonId;
        note.Date = input.Date;
        note.Tag = input.Tag;
        note.Text = input.Text.Trim();
        note.HarvestQuantity = input.HarvestQuantity;
        note.HarvestUnit = input.HarvestQuantity is null ? null : input.HarvestUnit?.Trim();
    }
}

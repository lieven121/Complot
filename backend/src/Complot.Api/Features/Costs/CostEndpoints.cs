using Complot.Api.Auth;
using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Api.Hosting;
using Complot.Domain.Costs;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Complot.Api.Features.Costs;

public sealed record CostCategoryDto(Guid Id, string Name, string Color);
public sealed record CostCategoryInput(string Name, string Color);

public sealed record InvoiceLineDto(string Description, Guid CategoryId, decimal Amount);

public sealed record InvoiceDto(
    Guid Id, DateOnly Date, string Supplier, string? Number, decimal Total, Guid? SiteId, string? Notes,
    bool HasAttachment, string? AttachmentName, List<InvoiceLineDto> Lines);

public sealed record InvoiceInput(DateOnly Date, string Supplier, string? Number, decimal Total, Guid? SiteId, string? Notes, List<InvoiceLineDto> Lines);

/// <summary>Costs per period are listed as <see cref="CostPeriod.Year"/> + <see cref="CostPeriod.Index"/>; the UI names them.</summary>
public sealed record CostPeriodDto(int Year, int Index, decimal Total, Dictionary<Guid, decimal> ByCategory);

/// <summary>The cost overview. <see cref="BedArea"/> (m², all sites) gives the cost per m².</summary>
public sealed record CostReportDto(
    int? Year, decimal Total, int InvoiceCount, Dictionary<Guid, decimal> ByCategory, List<CostPeriodDto> Periods,
    decimal YearToDate, decimal YearToDatePreviousYear, decimal AveragePerMonth, int Months, List<int> Years,
    Dictionary<Guid, Dictionary<int, decimal>> ByCategoryAndYear, double BedArea);

public sealed class CostCategoryInputValidator : AbstractValidator<CostCategoryInput>
{
    public CostCategoryInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Color).HexColor();
    }
}

public sealed class InvoiceInputValidator : AbstractValidator<InvoiceInput>
{
    /// <summary>Rounding margin when the lines are compared with the total.</summary>
    private const decimal Cent = 0.005m;

    public InvoiceInputValidator(ComplotDbContext db)
    {
        RuleFor(x => x.Supplier).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Number).MaximumLength(60);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.Total).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.SiteId).MustAsync((id, ct) => db.Sites.ExistsAsync(id!.Value, ct)).WithErrorCode("unknownSite").When(x => x.SiteId is not null);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty().MaximumLength(200);
            line.RuleFor(l => l.CategoryId).MustAsync((id, ct) => db.CostCategories.ExistsAsync(id, ct)).WithErrorCode("unknownCostCategory");
            line.RuleFor(l => l.Amount).NotEqual(0).InclusiveBetween(-1_000_000, 1_000_000);
        });
        RuleFor(x => x.Lines)
            .Must((x, lines, context) =>
            {
                var sum = lines.Sum(l => l.Amount);
                context.MessageFormatter.AppendArgument("lines", sum).AppendArgument("total", x.Total);
                return Math.Abs(sum - x.Total) < Cent;
            })
            .WithErrorCode("linesTotal")
            .When(x => x.Lines.Count > 0);
    }
}

public static class CostEndpoints
{
    /// <summary>Costs are not for view-only users or volunteers: reading needs edit rights somewhere, like changing.</summary>
    public static void MapCosts(this IEndpointRouteBuilder api)
    {
        var categories = api.MapGroup("/cost-categories").WithTags("Costs").RequireEditor();
        categories.MapGet("/", ListCategories).WithName("ListCostCategories");
        categories.MapPost("/", CreateCategory).WithName("CreateCostCategory").Validate<CostCategoryInput>();
        categories.MapPut("/{id:guid}", UpdateCategory).WithName("UpdateCostCategory").Validate<CostCategoryInput>();
        categories.MapDelete("/{id:guid}", DeleteCategory).WithName("DeleteCostCategory");

        var invoices = api.MapGroup("/invoices").WithTags("Costs").RequireEditor();
        invoices.MapGet("/", ListInvoices).WithName("ListInvoices");
        invoices.MapGet("/suppliers", Suppliers).WithName("ListInvoiceSuppliers");
        invoices.MapPost("/", CreateInvoice).WithName("CreateInvoice").Validate<InvoiceInput>();
        invoices.MapPut("/{id:guid}", UpdateInvoice).WithName("UpdateInvoice").Validate<InvoiceInput>();
        invoices.MapDelete("/{id:guid}", DeleteInvoice).WithName("DeleteInvoice");
        // Same-site cookies protect this upload; the multipart antiforgery token is not used by the SPA.
        invoices.MapPut("/{id:guid}/attachment", UploadAttachment).WithName("UploadInvoiceAttachment").DisableAntiforgery();
        invoices.MapGet("/{id:guid}/attachment", GetAttachment).WithName("GetInvoiceAttachment");
        invoices.MapDelete("/{id:guid}/attachment", DeleteAttachment).WithName("DeleteInvoiceAttachment");

        api.MapGet("/costs/report", Report).WithTags("Costs").WithName("GetCostReport").RequireEditor();
    }

    private static CostCategoryDto ToDto(CostCategory c) => new(c.Id, c.Name, c.Color);

    private static InvoiceDto ToDto(Invoice i) => new(
        i.Id, i.Date, i.Supplier, i.Number, i.Total, i.SiteId, i.Notes, i.AttachmentFileName is not null, i.AttachmentName,
        i.Lines.OrderByDescending(l => l.Amount).Select(l => new InvoiceLineDto(l.Description, l.CategoryId, l.Amount)).ToList());

    private static async Task<CostCategoryDto[]> ListCategories(ComplotDbContext db, CancellationToken ct) =>
        (await db.CostCategories.AsNoTracking().ToListAsync(ct)).OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).Select(ToDto).ToArray();

    private static async Task<Results<Created<CostCategoryDto>, ProblemHttpResult>> CreateCategory(CostCategoryInput input, ComplotDbContext db, CancellationToken ct)
    {
        var category = new CostCategory { Name = input.Name.Trim(), Color = input.Color.ToLowerInvariant() };
        db.CostCategories.Add(category);
        if (await db.SaveOrConflictAsync("costCategory.duplicate", ct) is { } conflict) return conflict;
        return TypedResults.Created($"/api/cost-categories/{category.Id}", ToDto(category));
    }

    private static async Task<Results<Ok<CostCategoryDto>, ProblemHttpResult>> UpdateCategory(
        Guid id, CostCategoryInput input, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.CostCategories.FindAsync([id], ct) is not { } category) return ApiProblems.NotFound();
        category.Name = input.Name.Trim();
        category.Color = input.Color.ToLowerInvariant();
        if (await db.SaveOrConflictAsync("costCategory.duplicate", ct) is { } conflict) return conflict;
        return TypedResults.Ok(ToDto(category));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteCategory(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.CostCategories.FindAsync([id], ct) is not { } category) return ApiProblems.NotFound();
        if (await db.InvoiceLines.AnyAsync(l => l.CategoryId == id, ct)) return ApiProblems.Conflict("costCategory.inUse");
        db.CostCategories.Remove(category);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Newest first; <paramref name="year"/> narrows to one year.</summary>
    private static async Task<InvoiceDto[]> ListInvoices(int? year, ComplotDbContext db, CancellationToken ct) =>
        (await db.Invoices.AsNoTracking().Include(i => i.Lines)
            .Where(i => year == null || (i.Date >= new DateOnly(year.Value, 1, 1) && i.Date <= new DateOnly(year.Value, 12, 31)))
            .ToListAsync(ct))
        .OrderByDescending(i => i.Date).ThenByDescending(i => i.CreatedAt)
        .Select(ToDto).ToArray();

    /// <summary>Names to suggest: earlier invoices and the catalog's suppliers.</summary>
    private static async Task<string[]> Suppliers(ComplotDbContext db, CancellationToken ct) =>
        (await db.Invoices.Select(i => i.Supplier).Distinct().ToListAsync(ct))
            .Union(await db.Suppliers.Select(s => s.Name).ToListAsync(ct), StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    private static async Task<Created<InvoiceDto>> CreateInvoice(InvoiceInput input, ComplotDbContext db, CancellationToken ct)
    {
        var invoice = new Invoice { Supplier = "" };
        Apply(input, invoice);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/invoices/{invoice.Id}", ToDto(invoice));
    }

    private static async Task<Results<Ok<InvoiceDto>, ProblemHttpResult>> UpdateInvoice(Guid id, InvoiceInput input, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, ct) is not { } invoice) return ApiProblems.NotFound();
        db.InvoiceLines.RemoveRange(invoice.Lines);
        Apply(input, invoice);
        // New lines carry their own id: add them explicitly, otherwise EF takes them for existing rows.
        db.InvoiceLines.AddRange(invoice.Lines);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(invoice));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteInvoice(Guid id, ComplotDbContext db, IOptions<StorageOptions> storage, CancellationToken ct)
    {
        if (await db.Invoices.FindAsync([id], ct) is not { } invoice) return ApiProblems.NotFound();
        StoredFiles.Delete(storage.Value.InvoiceDir, invoice.AttachmentFileName);
        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UploadAttachment(
        Guid id, IFormFile file, ComplotDbContext db, IOptions<StorageOptions> storage, CancellationToken ct)
    {
        if (await db.Invoices.FindAsync([id], ct) is not { } invoice) return ApiProblems.NotFound();
        if (StoredFiles.Check(file, FileKind.Document) is { } invalid) return invalid;
        invoice.AttachmentFileName = await StoredFiles.SaveAsync(file, FileKind.Document, storage.Value.InvoiceDir, invoice.Id, invoice.AttachmentFileName, ct);
        invoice.AttachmentName = Path.GetFileName(file.FileName) is { Length: > 0 and <= 200 } name ? name : null;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<PhysicalFileHttpResult, ProblemHttpResult>> GetAttachment(
        Guid id, ComplotDbContext db, IOptions<StorageOptions> storage, CancellationToken ct) =>
        await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct) is { AttachmentFileName: { } file } invoice
            ? StoredFiles.Serve(FileKind.Document, storage.Value.InvoiceDir, file, invoice.AttachmentName)
            : ApiProblems.NotFound();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAttachment(Guid id, ComplotDbContext db, IOptions<StorageOptions> storage, CancellationToken ct)
    {
        if (await db.Invoices.FindAsync([id], ct) is not { } invoice) return ApiProblems.NotFound();
        StoredFiles.Delete(storage.Value.InvoiceDir, invoice.AttachmentFileName);
        invoice.AttachmentFileName = null;
        invoice.AttachmentName = null;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary><paramref name="grouping"/>: "year", "quarter" or "month" (default).</summary>
    private static async Task<CostReportDto> Report(int? year, string? grouping, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var invoices = await db.Invoices.AsNoTracking().Include(i => i.Lines).ToListAsync(ct);
        var by = Enum.TryParse<CostGrouping>(grouping, ignoreCase: true, out var parsed) ? parsed : CostGrouping.Month;
        var r = CostReport.Build(invoices, year, by, time.Today());
        var bedArea = (await db.Beds.AsNoTracking().ToListAsync(ct)).Sum(b => b.Shape.Area);
        return new CostReportDto(
            r.Year, r.Total, r.InvoiceCount, new(r.ByCategory), r.Periods.Select(p => new CostPeriodDto(p.Year, p.Index, p.Total, new(p.ByCategory))).ToList(),
            r.YearToDate, r.YearToDatePreviousYear, r.AveragePerMonth, r.Months, [.. r.Years],
            r.ByCategoryAndYear.ToDictionary(c => c.Key, c => new Dictionary<int, decimal>(c.Value)), Math.Round(bedArea, 1));
    }

    private static void Apply(InvoiceInput input, Invoice invoice)
    {
        invoice.Date = input.Date;
        invoice.Supplier = input.Supplier.Trim();
        invoice.Number = string.IsNullOrWhiteSpace(input.Number) ? null : input.Number.Trim();
        invoice.Total = Math.Round(input.Total, 2);
        invoice.SiteId = input.SiteId;
        invoice.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        invoice.Lines = input.Lines
            .Select(l => new InvoiceLine { InvoiceId = invoice.Id, Description = l.Description.Trim(), CategoryId = l.CategoryId, Amount = Math.Round(l.Amount, 2) })
            .ToList();
    }
}

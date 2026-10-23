using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Work;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Work;

public sealed record PersonDto(Guid Id, string Name, string Color);

public sealed record PersonInput(string Name, string Color);

public sealed class PersonInputValidator : AbstractValidator<PersonInput>
{
    public PersonInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Color).NotEmpty().Matches("^#[0-9a-fA-F]{6}$").WithErrorCode("color");
    }
}

public static class PeopleEndpoints
{
    public static void MapPeople(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/people").WithTags("People");
        group.MapGet("/", async (ComplotDbContext db, CancellationToken ct) =>
            (await db.People.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct)).Select(ToDto).ToArray()).WithName("ListPeople");
        group.MapPost("/", Create).WithName("CreatePerson").Validate<PersonInput>();
        group.MapPut("/{id:guid}", Update).WithName("UpdatePerson").Validate<PersonInput>();
        group.MapDelete("/{id:guid}", Delete).WithName("DeletePerson");
    }

    private static PersonDto ToDto(Person p) => new(p.Id, p.Name, p.Color);

    private static async Task<Created<PersonDto>> Create(PersonInput input, ComplotDbContext db, CancellationToken ct)
    {
        var person = new Person { Name = input.Name.Trim(), Color = input.Color.ToLowerInvariant() };
        db.People.Add(person);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/people/{person.Id}", ToDto(person));
    }

    private static async Task<Results<Ok<PersonDto>, ProblemHttpResult>> Update(Guid id, PersonInput input, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.People.FindAsync([id], ct) is not { } person) return ApiProblems.NotFound();
        person.Name = input.Name.Trim();
        person.Color = input.Color.ToLowerInvariant();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(person));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.People.FindAsync([id], ct) is not { } person) return ApiProblems.NotFound();
        db.People.Remove(person); // assignments and note authors are cleared (SET NULL)
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}

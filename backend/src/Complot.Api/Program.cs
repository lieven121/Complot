using Complot.Api.Auth;
using Complot.Api.Data;
using Complot.Api.Features;
using Complot.Api.Features.Catalog;
using Complot.Api.Features.Garden;
using Complot.Api.Features.Planner;
using Complot.Api.Features.Work;
using Complot.Api.Hosting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddComplotHosting(builder.Configuration)
    .AddComplotData()
    .AddComplotAuth(builder.Configuration)
    .AddCatalog()
    .AddGarden()
    .AddPlanner(builder.Configuration)
    .AddWork();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapComplotEndpoints();
app.MapFallbackToFile("index.html").AllowAnonymous();

await app.RunAsync();

public partial class Program;

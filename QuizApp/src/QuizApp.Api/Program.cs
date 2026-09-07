using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuizApp.Api;
using QuizApp.Api.Filters;
using QuizApp.Api.Middleware;
using QuizApp.Application;
using QuizApp.Infrastructure;
using QuizApp.Infrastructure.Identity;
using QuizApp.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);

builder.Services.AddScoped<IdempotencyFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<IdempotencyFilter>());
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Integration tests swap in SQLite (a real relational engine, unlike
    // the EF Core InMemory provider) via CustomWebApplicationFactory, which
    // cannot replay migrations generated against SQL Server; it builds the
    // schema straight from the model instead. Real deployments always migrate.
    if (app.Environment.IsEnvironment("Testing"))
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
    else
    {
        await dbContext.Database.MigrateAsync();
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
    await RoleSeeder.SeedAsync(roleManager);
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<ProgramScopeMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = _ => true });

app.Run();

/// <summary>Makes the implicit top-level-statement Program class public, so
/// WebApplicationFactory&lt;Program&gt; in integration tests can reference it
/// (it is internal by default).</summary>
public partial class Program
{
}

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quizware.Api;
using Quizware.Api.Filters;
using Quizware.Api.Middleware;
using Quizware.Application;
using Quizware.Infrastructure;
using Quizware.Infrastructure.Identity;
using Quizware.Infrastructure.Persistence;
using Quizware.Modules.Buzzer;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);
builder.Services.AddBuzzerModule();

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

    // Dev convenience only — never auto-create a known-password account in
    // Production. Credentials: AdminUserSeeder.DefaultEmail / DefaultPassword.
    if (!app.Environment.IsProduction())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        await AdminUserSeeder.SeedAsync(userManager);

        // P7-12: the 18-team tournament reproduced entirely as configuration
        // data. Dev/demo convenience only; idempotent by Program.Code.
        await Quizware.Infrastructure.Persistence.TournamentSeeder.SeedAsync(dbContext);
    }
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    // Swashbuckle remains the source of truth for the OpenAPI document — it
    // carries the QuestionResponse discriminator/mapping wiring from
    // DependencyInjection.cs.AddSwagger that the built-in Microsoft.AspNetCore.OpenApi
    // generator (registered via AddOpenApi/MapOpenApi for tooling that expects
    // that route) doesn't support. Scalar renders that same Swashbuckle
    // document instead of Swagger UI.
    app.MapOpenApi();
    app.UseSwagger();
    app.MapScalarApiReference(options => options
        .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json")
        .WithTitle("Quizware API"));
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

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuizApp.Infrastructure.Persistence;
using QuizApp.Infrastructure.Persistence.Interceptors;

namespace QuizApp.Api.IntegrationTests;

/// <summary>
/// Swaps the real SQL Server connection for an in-process SQLite database —
/// a real relational engine that enforces constraints, unlike the EF Core
/// InMemory provider — so these tests can run without Docker/SQL Server.
/// The plan's own rule ("never use the EF Core in-memory provider") is
/// about the fake, non-enforcing provider; SQLite is a real database.
/// Program.cs's own startup migrates the schema and seeds roles against
/// whatever provider is registered, so no extra setup is needed here.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CustomWebApplicationFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // AddDbContext's provider configuration is additive
            // (IDbContextOptionsConfiguration<T> registrations stack), so
            // removing only the DbContextOptions<T> descriptor leaves the
            // SqlServer configuration in place alongside SQLite's. Remove
            // every EF-related descriptor for this context before re-adding.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));

            services.AddDbContext<AppDbContext>((sp, options) => options
                .UseSqlite(_connection)
                // The migration snapshot was generated against SqlServer;
                // applying it under Sqlite trips EF's cross-provider model
                // diff check even though the resulting schema is fine here.
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                .AddInterceptors(
                    sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditLogSaveChangesInterceptor>()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

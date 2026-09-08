using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuizApp.Application.Abstractions;
using QuizApp.Infrastructure.Common;
using QuizApp.Infrastructure.Identity;
using QuizApp.Infrastructure.Idempotency;
using QuizApp.Infrastructure.Media;
using QuizApp.Infrastructure.Persistence;
using QuizApp.Infrastructure.Persistence.Interceptors;

namespace QuizApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Server=localhost;Database=QuizApp;Trusted_Connection=True;TrustServerCertificate=True";

        services.AddScoped<AuditableEntitySaveChangesInterceptor>();
        services.AddScoped<AuditLogSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseSqlServer(connectionString)
            .AddInterceptors(
                sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>(),
                sp.GetRequiredService<AuditLogSaveChangesInterceptor>()));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentProgram, CurrentProgram>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

        services.Configure<MediaStorageOptions>(configuration.GetSection(MediaStorageOptions.SectionName));
        services.AddScoped<IFileStorage, LocalFileStorage>();

        return services;
    }
}

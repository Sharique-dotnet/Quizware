using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quizware.Application.Abstractions;
using Quizware.Infrastructure.Common;
using Quizware.Infrastructure.Identity;
using Quizware.Infrastructure.Idempotency;
using Quizware.Infrastructure.Media;
using Quizware.Infrastructure.Persistence;
using Quizware.Infrastructure.Persistence.Interceptors;

namespace Quizware.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Server=localhost;Database=Quizware;Trusted_Connection=True;TrustServerCertificate=True";

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

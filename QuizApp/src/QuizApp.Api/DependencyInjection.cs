using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuizApp.Application.Authorization;
using QuizApp.Infrastructure.Persistence;

namespace QuizApp.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        AddJwtAuthentication(services, configuration);
        AddAuthorizationPolicies(services);
        AddSwagger(services);

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");

        return services;
    }

    private static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(Infrastructure.Identity.JwtOptions.SectionName);
        var signingKey = jwtSection["SigningKey"] ?? string.Empty;

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
    }

    private static void AddAuthorizationPolicies(IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.CanManageProgram, p => p.RequireRole(Roles.SuperAdmin, Roles.ProgramAdmin))
            .AddPolicy(Policies.CanManageQuestions, p => p.RequireRole(Roles.SuperAdmin, Roles.ProgramAdmin, Roles.QuestionAuthor))
            .AddPolicy(Policies.CanOperateMatch, p => p.RequireRole(Roles.SuperAdmin, Roles.ProgramAdmin, Roles.Operator))
            .AddPolicy(Policies.CanRecordAnswer, p => p.RequireRole(Roles.SuperAdmin, Roles.ProgramAdmin, Roles.Operator, Roles.Scorer))
            .AddPolicy(Policies.CanAdjustScore, p => p.RequireRole(Roles.SuperAdmin, Roles.ProgramAdmin))
            .AddPolicy(Policies.CanDisqualify, p => p.RequireRole(Roles.SuperAdmin, Roles.ProgramAdmin))
            .AddPolicy(Policies.CanResolveTie, p => p.RequireRole(Roles.SuperAdmin, Roles.ProgramAdmin))
            .AddPolicy(Policies.CanViewLive, p => p.RequireAuthenticatedUser())
            .AddPolicy(Policies.DisplayOnly, p => p.RequireRole(Roles.Display));
    }

    private static void AddSwagger(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "QuizApp API", Version = "v1" });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste a JWT access token — the 'Bearer ' prefix is added automatically.",
            });
        });
    }
}

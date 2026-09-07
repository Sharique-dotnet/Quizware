using System.Text;
using FluentValidation;
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

        // Per-format request validators (05-API-Design.md §5.8) — this
        // assembly only; MediatR command/query validators live in Application
        // and are picked up by ValidationBehavior<,> instead.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

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
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "QuizApp API",
                Version = "v1",
                Description = "Contract for 05-API-Design.md. Most actions return 501 until their " +
                    "phase implements them (see docs/Implementation-Plan.md) — the route, request " +
                    "and response shapes are frozen so the Angular client can be generated now.",
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste a JWT access token — the 'Bearer ' prefix is added automatically.",
            });

            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(DependencyInjection).Assembly.GetName().Name}.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Emits `oneOf` + discriminator for QuestionResponse and its 10
            // per-format subtypes (the [JsonDerivedType] list on
            // QuestionResponse) — this is what makes the generated TS client
            // a discriminated union instead of a single flattened type
            // (05-API-Design.md §5.8). Swashbuckle does not walk
            // [JsonDerivedType] attributes on its own; SelectSubTypesUsing
            // supplies the same list explicitly.
            options.UseOneOfForPolymorphism();
            options.SelectDiscriminatorNameUsing(_ => "formatCode");
            options.SelectSubTypesUsing(baseType => baseType == typeof(Contracts.V1.Questions.QuestionResponse)
                ? baseType.Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(baseType))
                : []);
        });
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Quizware.Api.Contracts.V1.Programs;
using Quizware.Api.Contracts.V1.Questions;
using Quizware.Api.Controllers.v1;
using Quizware.Application.Authorization;
using Quizware.Infrastructure.Identity;

namespace Quizware.Api.IntegrationTests;

/// <summary>Phase 6e: POST .../questions/media is no longer a 501 stub.</summary>
public class MediaEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MediaEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateUnscopedSuperAdminClientAsync()
    {
        var email = $"media-admin-{Guid.NewGuid():N}@quizapp.test";
        const string password = "P@ssw0rd123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, FullName = "Test Admin", IsActive = true, EmailConfirmed = true };
            await userManager.CreateAsync(user, password);
            await userManager.AddToRolesAsync(user, [Roles.SuperAdmin]);
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateProgramScopedClientAsync(Guid programId, params string[] roles)
    {
        var email = $"media-user-{Guid.NewGuid():N}@quizapp.test";
        const string password = "P@ssw0rd123!";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new AppUser { UserName = email, Email = email, FullName = "Test User", IsActive = true, EmailConfirmed = true };
        await userManager.CreateAsync(user, password);

        var token = tokenService.GenerateAccessToken(user, roles, programId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> CreateProgramAsync(HttpClient superAdminClient)
    {
        var response = await superAdminClient.PostAsJsonAsync(
            "/api/v1/programs", new CreateProgramRequest($"P-{Guid.NewGuid():N}", "Test Program", null));
        var program = await response.Content.ReadFromJsonAsync<ProgramDetailResponse>();
        return program!.Id;
    }

    private static MultipartFormDataContent BuildUpload(byte[] bytes, string fileName, bool shared = false)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", fileName);
        content.Add(new StringContent(shared ? "true" : "false"), "shared");
        return content;
    }

    [Fact]
    public async Task Upload_ValidPng_Succeeds()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

        var response = await client.PostAsync($"/api/v1/programs/{programId}/questions/media", BuildUpload(pngBytes, "photo.png"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var media = await response.Content.ReadFromJsonAsync<MediaAssetResponse>();
        media!.IsValidated.Should().BeTrue();
        media.MediaType.Should().Be("Image");
    }

    [Fact]
    public async Task Upload_RenamedExeAsJpg_IsRejected()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        // MZ header — a real Windows executable — renamed to .jpg.
        var exeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };

        var response = await client.PostAsync($"/api/v1/programs/{programId}/questions/media", BuildUpload(exeBytes, "totally-a-photo.jpg"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_DisallowedExtension_IsRejected()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var response = await client.PostAsync(
            $"/api/v1/programs/{programId}/questions/media", BuildUpload([0x01, 0x02], "script.exe"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_SameBytesTwice_Deduplicates()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xAB, 0xCD };

        var first = await (await client.PostAsync($"/api/v1/programs/{programId}/questions/media", BuildUpload(pngBytes, "a.png")))
            .Content.ReadFromJsonAsync<MediaAssetResponse>();
        var secondResponse = await client.PostAsync($"/api/v1/programs/{programId}/questions/media", BuildUpload(pngBytes, "b.png"));
        var second = await secondResponse.Content.ReadFromJsonAsync<MediaAssetResponse>();

        second!.Id.Should().Be(first!.Id);
        second.WasDeduplicated.Should().BeTrue();
    }

    [Fact]
    public async Task Upload_SharedAsNonSuperAdmin_ReturnsForbidden()
    {
        var admin = await CreateUnscopedSuperAdminClientAsync();
        var programId = await CreateProgramAsync(admin);
        var client = await CreateProgramScopedClientAsync(programId, Roles.ProgramAdmin);

        var response = await client.PostAsync(
            $"/api/v1/programs/{programId}/questions/media",
            BuildUpload([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "shared.png", shared: true));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

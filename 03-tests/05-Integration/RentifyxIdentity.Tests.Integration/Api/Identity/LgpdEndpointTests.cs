using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RentifyxIdentity.Application.Features.Identity;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Application.Features.Identity.User.ExportData;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Tests.Common.Builders;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Api.Identity;

[Collection("Integration")]
public sealed class LgpdEndpointTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string RegisterEndpoint = "/api/v1/auth/register";
    private const string VerifyEmailEndpoint = "/api/v1/auth/verify-email";
    private const string ForgotPasswordEndpoint = "/api/v1/auth/forgot-password";
    private const string GetProfileEndpoint = "/api/v1/users/me";
    private const string DeleteAccountEndpoint = "/api/v1/users/me";
    private const string ExportDataEndpoint = "/api/v1/users/me/data-export";

    private async Task<(Guid UserId, RegisterUserRequest Request)> RegisterAndVerifyAsync()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        HttpResponseMessage registerResponse = await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);
        UserResponse user = (await registerResponse.Content.ReadFromJsonAsync<UserResponse>())!;

        string rawToken = factory.UserRepository.SentVerificationEmails
            .First(e => e.Recipient == registerRequest.Email.ToLowerInvariant())
            .Token;

        await _client.PostAsJsonAsync(VerifyEmailEndpoint, new VerifyEmailRequest(registerRequest.Email, rawToken));

        return (user.Id, registerRequest);
    }

    private void AuthenticateAs(Guid userId) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

    private void RemoveAuthentication() =>
        _client.DefaultRequestHeaders.Authorization = null;

    [Fact]
    public async Task GetProfile_Authenticated_Returns200()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        HttpResponseMessage response = await _client.GetAsync(GetProfileEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        UserResponse? profile = await response.Content.ReadFromJsonAsync<UserResponse>();
        profile.Should().NotBeNull();
        profile!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_Returns401()
    {
        RemoveAuthentication();

        HttpResponseMessage response = await _client.GetAsync(GetProfileEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExportData_Authenticated_Returns200WithMaskedTaxId()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        HttpResponseMessage response = await _client.GetAsync(ExportDataEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        UserDataExportResponse? export = await response.Content.ReadFromJsonAsync<UserDataExportResponse>();
        export.Should().NotBeNull();
        export!.Id.Should().Be(userId);
        export.TaxId.Should().Be("***.***.***-**");
    }

    [Fact]
    public async Task ExportData_Unauthenticated_Returns401()
    {
        RemoveAuthentication();

        HttpResponseMessage response = await _client.GetAsync(ExportDataEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAccount_Authenticated_Returns204()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        HttpResponseMessage response = await _client.DeleteAsync(DeleteAccountEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage profileResponse = await _client.GetAsync(GetProfileEndpoint);
        profileResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAccount_Unauthenticated_Returns401()
    {
        RemoveAuthentication();

        HttpResponseMessage response = await _client.DeleteAsync(DeleteAccountEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAccount_AlreadyDeleted_Returns409()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        await _client.DeleteAsync(DeleteAccountEndpoint);

        HttpResponseMessage response = await _client.DeleteAsync(DeleteAccountEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetProfile_Authenticated_AuditsProfileAccessed()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        await _client.GetAsync(GetProfileEndpoint);

        factory.AuditLogService.Entries
            .Should().Contain(e => e.UserId == userId && e.EventType == AuditEvents.ProfileAccessed);
    }

    [Fact]
    public async Task ExportData_Authenticated_AuditsDataExported()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        await _client.GetAsync(ExportDataEndpoint);

        factory.AuditLogService.Entries
            .Should().Contain(e => e.UserId == userId && e.EventType == AuditEvents.DataExported);
    }

    [Fact]
    public async Task DeleteAccount_Authenticated_AuditsAccountDeleted()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        await _client.DeleteAsync(DeleteAccountEndpoint);

        factory.AuditLogService.Entries
            .Should().Contain(e => e.UserId == userId && e.EventType == AuditEvents.AccountDeleted);
    }

    [Fact]
    public async Task ForgotPassword_DeletedAccount_Returns204()
    {
        (Guid userId, RegisterUserRequest registered) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);
        await _client.DeleteAsync(DeleteAccountEndpoint);
        RemoveAuthentication();

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            ForgotPasswordEndpoint,
            new ForgotPasswordRequest(registered.Email));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

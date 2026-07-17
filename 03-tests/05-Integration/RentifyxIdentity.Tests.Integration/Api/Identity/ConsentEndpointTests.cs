using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using FluentAssertions;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Application.Features.Identity.User.Consent;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Api.Identity;

// Not part of [Collection("Integration")] on purpose: that collection shares one
// CustomWebApplicationFactory (and its process-wide rate limiter bucket) across every test
// class, and this class alone adds enough real HTTP calls to exhaust the shared 100-req/60s
// window and cause unrelated tests to fail with 429. IClassFixture gives it its own instance.
public sealed class ConsentEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string RegisterEndpoint = "/api/v1/auth/register";
    private const string VerifyEmailEndpoint = "/api/v1/auth/verify-email";
    private const string LoginEndpoint = "/api/v1/auth/login";
    private const string DeleteAccountEndpoint = "/api/v1/users/me";
    private const string ConsentEndpoint = "/api/v1/users/me/consent";

    private static int _taxIdCounter;

    private async Task<(Guid UserId, RegisterUserRequest Request)> RegisterAndVerifyAsync()
    {
        int sequence = Interlocked.Increment(ref _taxIdCounter);

        RegisterUserRequest registerRequest = new(
            $"consent-{Guid.NewGuid():N}@example.com",
            $"7{sequence:D10}",
            TestConstants.ValidPassword,
            "Owner",
            true);
        HttpResponseMessage registerResponse = await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);
        Application.Features.Identity.UserResponse user =
            (await registerResponse.Content.ReadFromJsonAsync<Application.Features.Identity.UserResponse>())!;

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
    public async Task GetConsent_Authenticated_ReturnsEssentialGrantedFromRegistration()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        HttpResponseMessage response = await _client.GetAsync(ConsentEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConsentResponse? consent = await response.Content.ReadFromJsonAsync<ConsentResponse>();
        consent.Should().NotBeNull();
        consent!.EssentialGranted.Should().BeTrue();
        consent.MarketingGranted.Should().BeFalse();
    }

    [Fact]
    public async Task GetConsent_Unauthenticated_Returns401()
    {
        RemoveAuthentication();

        HttpResponseMessage response = await _client.GetAsync(ConsentEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateConsent_RevokeEssential_SuspendsAccount()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            ConsentEndpoint,
            new { Purpose = "Essential", Granted = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConsentResponse? consent = await response.Content.ReadFromJsonAsync<ConsentResponse>();
        consent!.EssentialGranted.Should().BeFalse();
        consent.EssentialRevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateConsent_GrantMarketing_ReturnsGrantedTrue()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            ConsentEndpoint,
            new { Purpose = "Marketing", Granted = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConsentResponse? consent = await response.Content.ReadFromJsonAsync<ConsentResponse>();
        consent!.MarketingGranted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateConsent_RevokeMarketing_ReturnsGrantedFalse()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);
        await _client.PutAsJsonAsync(ConsentEndpoint, new { Purpose = "Marketing", Granted = true });

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            ConsentEndpoint,
            new { Purpose = "Marketing", Granted = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConsentResponse? consent = await response.Content.ReadFromJsonAsync<ConsentResponse>();
        consent!.MarketingGranted.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateConsent_GrantEssentialAfterRevoke_ReactivatesAccount()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);
        await _client.PutAsJsonAsync(ConsentEndpoint, new { Purpose = "Essential", Granted = false });

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            ConsentEndpoint,
            new { Purpose = "Essential", Granted = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConsentResponse? consent = await response.Content.ReadFromJsonAsync<ConsentResponse>();
        consent!.EssentialGranted.Should().BeTrue();
        consent.EssentialRevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateConsent_Unauthenticated_Returns401()
    {
        RemoveAuthentication();

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            ConsentEndpoint,
            new { Purpose = "Essential", Granted = false });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateConsent_DeletedAccount_Returns404()
    {
        (Guid userId, _) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);
        await _client.DeleteAsync(DeleteAccountEndpoint);

        HttpResponseMessage response = await _client.PutAsJsonAsync(
            ConsentEndpoint,
            new { Purpose = "Essential", Granted = false });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EndToEnd_RevokeEssentialThenLoginFails_GrantEssentialThenLoginSucceeds()
    {
        (Guid userId, RegisterUserRequest registered) = await RegisterAndVerifyAsync();
        AuthenticateAs(userId);

        await _client.PutAsJsonAsync(ConsentEndpoint, new { Purpose = "Essential", Granted = false });
        RemoveAuthentication();

        HttpResponseMessage lockedLoginResponse = await _client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginRequest(registered.Email, registered.Password));
        lockedLoginResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        AuthenticateAs(userId);
        await _client.PutAsJsonAsync(ConsentEndpoint, new { Purpose = "Essential", Granted = true });
        RemoveAuthentication();

        HttpResponseMessage successfulLoginResponse = await _client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginRequest(registered.Email, registered.Password));
        successfulLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Logout.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Tests.Common.Builders;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Api.Identity;

[Collection("Integration")]
public sealed class LogoutEndpointTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string RegisterEndpoint = "/api/v1/auth/register";
    private const string VerifyEmailEndpoint = "/api/v1/auth/verify-email";
    private const string LoginEndpoint = "/api/v1/auth/login";
    private const string LogoutEndpoint = "/api/v1/auth/logout";

    private async Task<(string Email, string RefreshToken)> RegisterVerifyLoginAsync()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);

        string rawToken = factory.EmailService.SentVerificationEmails
            .First(e => e.Recipient == registerRequest.Email.ToLowerInvariant())
            .Token;

        await _client.PostAsJsonAsync(VerifyEmailEndpoint, new VerifyEmailRequest(registerRequest.Email, rawToken));

        HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginRequest(registerRequest.Email, registerRequest.Password));

        string loginContent = await loginResponse.Content.ReadAsStringAsync();
        JsonDocument loginDoc = JsonDocument.Parse(loginContent);
        string refreshToken = loginDoc.RootElement.GetProperty("refreshToken").GetString()!;

        return (registerRequest.Email, refreshToken);
    }

    [Fact]
    public async Task Logout_WithValidToken_Returns204()
    {
        (string email, string refreshToken) = await RegisterVerifyLoginAsync();

        LogoutRequest request = new(email, refreshToken);
        HttpResponseMessage response = await _client.PostAsJsonAsync(LogoutEndpoint, request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_CalledTwice_Returns204BothTimes()
    {
        (string email, string refreshToken) = await RegisterVerifyLoginAsync();

        LogoutRequest request = new(email, refreshToken);

        HttpResponseMessage first = await _client.PostAsJsonAsync(LogoutEndpoint, request);
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage second = await _client.PostAsJsonAsync(LogoutEndpoint, request);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

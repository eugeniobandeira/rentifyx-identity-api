using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.RefreshToken.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Tests.Common.Builders;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Api.Identity;

[Collection("Integration")]
public sealed class RefreshTokenEndpointTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string RegisterEndpoint = "/api/v1/auth/register";
    private const string VerifyEmailEndpoint = "/api/v1/auth/verify-email";
    private const string LoginEndpoint = "/api/v1/auth/login";
    private const string RefreshEndpoint = "/api/v1/auth/refresh";

    private async Task<(string Email, string Password, string RefreshToken)> RegisterVerifyLoginAsync()
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

        return (registerRequest.Email, registerRequest.Password, refreshToken);
    }

    [Fact]
    public async Task Refresh_WithValidToken_Returns200WithNewTokens()
    {
        (string email, _, string refreshToken) = await RegisterVerifyLoginAsync();

        RefreshTokenRequest request = new(email, refreshToken);
        HttpResponseMessage response = await _client.PostAsJsonAsync(RefreshEndpoint, request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(content);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("accessToken", out JsonElement accessToken).Should().BeTrue();
        accessToken.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("refreshToken", out JsonElement newRefreshToken).Should().BeTrue();
        newRefreshToken.GetString().Should().NotBeNullOrEmpty();
        newRefreshToken.GetString().Should().NotBe(refreshToken);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_Returns422()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);

        string rawToken = factory.EmailService.SentVerificationEmails
            .First(e => e.Recipient == registerRequest.Email.ToLowerInvariant())
            .Token;

        await _client.PostAsJsonAsync(VerifyEmailEndpoint, new VerifyEmailRequest(registerRequest.Email, rawToken));

        RefreshTokenRequest request = new(registerRequest.Email, "wrong-refresh-token");
        HttpResponseMessage response = await _client.PostAsJsonAsync(RefreshEndpoint, request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

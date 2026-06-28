using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Tests.Common.Builders;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Api.Identity;

[Collection("Integration")]
public sealed class LoginEndpointTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string RegisterEndpoint = "/api/v1/auth/register";
    private const string VerifyEmailEndpoint = "/api/v1/auth/verify-email";
    private const string LoginEndpoint = "/api/v1/auth/login";

    private async Task<RegisterUserRequest> RegisterAndVerifyAsync()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);

        string rawToken = factory.EmailService.SentVerificationEmails
            .First(e => e.Recipient == registerRequest.Email.ToLowerInvariant())
            .Token;

        VerifyEmailRequest verifyRequest = new(registerRequest.Email, rawToken);
        await _client.PostAsJsonAsync(VerifyEmailEndpoint, verifyRequest);

        return registerRequest;
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokens()
    {
        RegisterUserRequest registered = await RegisterAndVerifyAsync();

        LoginRequest loginRequest = new(registered.Email, registered.Password);
        HttpResponseMessage response = await _client.PostAsJsonAsync(LoginEndpoint, loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(content);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("accessToken", out JsonElement accessToken).Should().BeTrue();
        accessToken.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("refreshToken", out JsonElement refreshToken).Should().BeTrue();
        refreshToken.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("user", out JsonElement userProp).Should().BeTrue();
        userProp.TryGetProperty("status", out JsonElement statusProp).Should().BeTrue();
        statusProp.GetString().Should().Be(TestConstants.StatusActive);
    }

    [Fact]
    public async Task Login_WithoutEmailVerification_Returns422()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);

        LoginRequest loginRequest = new(registerRequest.Email, registerRequest.Password);
        HttpResponseMessage response = await _client.PostAsJsonAsync(LoginEndpoint, loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns422()
    {
        RegisterUserRequest registered = await RegisterAndVerifyAsync();

        LoginRequest loginRequest = new(registered.Email, "wrong-password-!!");
        HttpResponseMessage response = await _client.PostAsJsonAsync(LoginEndpoint, loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

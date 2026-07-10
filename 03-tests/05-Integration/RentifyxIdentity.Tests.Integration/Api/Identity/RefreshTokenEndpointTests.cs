using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RentifyxIdentity.Application.Features.Identity.Auth.Login.Request;
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

    private async Task<string> RegisterVerifyLoginAsync()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);

        string rawToken = factory.EmailService.SentVerificationEmails
            .First(e => e.Recipient == registerRequest.Email.ToLowerInvariant())
            .Token;

        await _client.PostAsJsonAsync(VerifyEmailEndpoint, new VerifyEmailRequest(registerRequest.Email, rawToken));

        await _client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginRequest(registerRequest.Email, registerRequest.Password));

        return registerRequest.Email;
    }

    [Fact]
    public async Task Refresh_WithValidCookie_Returns200WithNewAccessTokenAndRotatesCookie()
    {
        string email = await RegisterVerifyLoginAsync();

        HttpResponseMessage response = await _client.PostAsJsonAsync(RefreshEndpoint, new { email });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(content);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("accessToken", out JsonElement accessToken).Should().BeTrue();
        accessToken.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("refreshToken", out _).Should().BeFalse();

        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.StartsWith("refreshToken=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns422()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);

        string rawToken = factory.EmailService.SentVerificationEmails
            .First(e => e.Recipient == registerRequest.Email.ToLowerInvariant())
            .Token;

        await _client.PostAsJsonAsync(VerifyEmailEndpoint, new VerifyEmailRequest(registerRequest.Email, rawToken));

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            RefreshEndpoint,
            new { email = registerRequest.Email });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Refresh_WithInvalidCookie_Returns422()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);

        string rawToken = factory.EmailService.SentVerificationEmails
            .First(e => e.Recipient == registerRequest.Email.ToLowerInvariant())
            .Token;

        await _client.PostAsJsonAsync(VerifyEmailEndpoint, new VerifyEmailRequest(registerRequest.Email, rawToken));

        using HttpRequestMessage request = new(HttpMethod.Post, RefreshEndpoint)
        {
            Content = JsonContent.Create(new { email = registerRequest.Email })
        };
        request.Headers.Add("Cookie", "refreshToken=wrong-refresh-token");

        HttpResponseMessage response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

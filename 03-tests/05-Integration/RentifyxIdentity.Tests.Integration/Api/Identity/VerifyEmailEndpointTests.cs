using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Tests.Common.Builders;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Api.Identity;

[Collection("Integration")]
public sealed class VerifyEmailEndpointTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string RegisterEndpoint = "/api/v1/auth/register";
    private const string VerifyEmailEndpoint = "/api/v1/auth/verify-email";

    [Fact]
    public async Task VerifyEmail_WithValidToken_Returns200AndActiveStatus()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        HttpResponseMessage registerResponse = await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        string rawToken = factory.EmailService.SentVerificationEmails
            .First(e => e.Recipient == registerRequest.Email.ToLowerInvariant())
            .Token;

        VerifyEmailRequest verifyRequest = new(registerRequest.Email, rawToken);
        HttpResponseMessage verifyResponse = await _client.PostAsJsonAsync(VerifyEmailEndpoint, verifyRequest);

        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        string content = await verifyResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Active");
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_Returns400()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);

        VerifyEmailRequest verifyRequest = new(registerRequest.Email, "invalid-wrong-token");
        HttpResponseMessage response = await _client.PostAsJsonAsync(VerifyEmailEndpoint, verifyRequest);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task VerifyEmail_WithExpiredToken_Returns400()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);

        string rawToken = factory.EmailService.SentVerificationEmails
            .First(e => e.Recipient == registerRequest.Email.ToLowerInvariant())
            .Token;

        UserEntity? user = await factory.UserRepository.GetByEmailAsync(registerRequest.Email);
        user!.SetEmailVerificationToken(user.EmailVerificationTokenHash!, DateTimeOffset.UtcNow.AddHours(-1));

        VerifyEmailRequest verifyRequest = new(registerRequest.Email, rawToken);
        HttpResponseMessage response = await _client.PostAsJsonAsync(VerifyEmailEndpoint, verifyRequest);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

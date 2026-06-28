using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RentifyxIdentity.Application.Features.Identity.Auth.ForgotPassword.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.ResetPassword.Request;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;
using RentifyxIdentity.Tests.Common.Builders;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Api.Identity;

[Collection("Integration")]
public sealed class PasswordResetEndpointTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string RegisterEndpoint = "/api/v1/auth/register";
    private const string VerifyEmailEndpoint = "/api/v1/auth/verify-email";
    private const string ForgotPasswordEndpoint = "/api/v1/auth/forgot-password";
    private const string ResetPasswordEndpoint = "/api/v1/auth/reset-password";

    private async Task<RegisterUserRequest> RegisterAndVerifyAsync()
    {
        RegisterUserRequest registerRequest = new RegisterUserRequestBuilder().Build();
        await _client.PostAsJsonAsync(RegisterEndpoint, registerRequest);

        string rawToken = factory.EmailService.SentVerificationEmails
            .First(e => e.Recipient == registerRequest.Email.ToLowerInvariant())
            .Token;

        await _client.PostAsJsonAsync(VerifyEmailEndpoint, new VerifyEmailRequest(registerRequest.Email, rawToken));

        return registerRequest;
    }

    [Fact]
    public async Task ForgotPassword_ThenResetPassword_Returns204()
    {
        RegisterUserRequest registered = await RegisterAndVerifyAsync();

        HttpResponseMessage forgotResponse = await _client.PostAsJsonAsync(
            ForgotPasswordEndpoint,
            new ForgotPasswordRequest(registered.Email));

        forgotResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        string resetToken = factory.EmailService.SentPasswordResetEmails
            .First(e => e.Recipient == registered.Email.ToLowerInvariant())
            .Token;

        HttpResponseMessage resetResponse = await _client.PostAsJsonAsync(
            ResetPasswordEndpoint,
            new ResetPasswordRequest(registered.Email, resetToken, "NewP@ssword456!"));

        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_Returns204()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync(
            ForgotPasswordEndpoint,
            new ForgotPasswordRequest("unknown@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_WithWrongToken_Returns422()
    {
        RegisterUserRequest registered = await RegisterAndVerifyAsync();

        await _client.PostAsJsonAsync(
            ForgotPasswordEndpoint,
            new ForgotPasswordRequest(registered.Email));

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            ResetPasswordEndpoint,
            new ResetPasswordRequest(registered.Email, "wrong-token", "NewP@ssword456!"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

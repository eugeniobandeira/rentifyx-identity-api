using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RentifyxIdentity.Application.Features.Identity.Auth.Register.Request;
using RentifyxIdentity.Tests.Common.Builders;
using RentifyxIdentity.Tests.Common.Constants;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Api.Identity;

[Collection("Integration")]
public sealed class RegisterEndpointTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string RegisterEndpoint = "/api/v1/auth/register";

    [Fact]
    public async Task RegisterUser_WithValidRequest_Returns201AndUserResponse()
    {
        RegisterUserRequest request = new RegisterUserRequestBuilder().Build();

        HttpResponseMessage response = await _client.PostAsJsonAsync(RegisterEndpoint, request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        string content = await response.Content.ReadAsStringAsync();
        JsonDocument doc = JsonDocument.Parse(content);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("id", out JsonElement idProp).Should().BeTrue();
        idProp.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("email", out JsonElement emailProp).Should().BeTrue();
        emailProp.GetString().Should().Be(request.Email.ToLowerInvariant());

        root.TryGetProperty("role", out JsonElement roleProp).Should().BeTrue();
        roleProp.GetString().Should().Be(request.Role);

        root.TryGetProperty("status", out JsonElement statusProp).Should().BeTrue();
        statusProp.GetString().Should().Be(TestConstants.StatusPendingVerification);

        root.TryGetProperty("createdAt", out JsonElement createdAtProp).Should().BeTrue();
        createdAtProp.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("taxId", out _).Should().BeFalse();
        root.TryGetProperty("passwordHash", out _).Should().BeFalse();

        factory.UserRepository.SentVerificationEmails
            .Should().Contain(e => e.Recipient == request.Email.ToLowerInvariant());
    }

    [Fact]
    public async Task RegisterUser_WithDuplicateEmail_Returns409WithEmailAlreadyRegistered()
    {
        string sharedEmail = new RegisterUserRequestBuilder().Build().Email;

        RegisterUserRequest firstRequest = new RegisterUserRequestBuilder()
            .WithEmail(sharedEmail)
            .Build();

        RegisterUserRequest secondRequest = new RegisterUserRequestBuilder()
            .WithEmail(sharedEmail)
            .Build();

        HttpResponseMessage firstResponse = await _client.PostAsJsonAsync(RegisterEndpoint, firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage secondResponse = await _client.PostAsJsonAsync(RegisterEndpoint, secondRequest);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        string content = await secondResponse.Content.ReadAsStringAsync();
        content.Should().Contain(TestConstants.EmailAlreadyRegisteredTitle);
    }

    [Fact]
    public async Task RegisterUser_WithDuplicateTaxId_Returns409WithTaxIdAlreadyRegistered()
    {
        string sharedTaxId = new RegisterUserRequestBuilder().Build().TaxId;

        RegisterUserRequest firstRequest = new RegisterUserRequestBuilder()
            .WithTaxId(sharedTaxId)
            .Build();

        RegisterUserRequest secondRequest = new RegisterUserRequestBuilder()
            .WithTaxId(sharedTaxId)
            .Build();

        HttpResponseMessage firstResponse = await _client.PostAsJsonAsync(RegisterEndpoint, firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage secondResponse = await _client.PostAsJsonAsync(RegisterEndpoint, secondRequest);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        string content = await secondResponse.Content.ReadAsStringAsync();
        content.Should().Contain(TestConstants.TaxIdAlreadyRegisteredTitle);
    }

    [Fact]
    public async Task RegisterUser_WithEmptyBody_Returns422WithAllFieldErrors()
    {
        RegisterUserRequest request = new RegisterUserRequestBuilder()
            .WithEmail(string.Empty)
            .WithTaxId(string.Empty)
            .WithPassword(string.Empty)
            .WithRole(string.Empty)
            .Build();

        HttpResponseMessage response = await _client.PostAsJsonAsync(RegisterEndpoint, request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Email");
        content.Should().Contain("TaxId");
        content.Should().Contain("Password");
        content.Should().Contain("Role");
    }

    [Fact]
    public async Task RegisterUser_WithPasswordTooShort_Returns422WithPasswordMinLengthError()
    {
        RegisterUserRequest request = new RegisterUserRequestBuilder()
            .WithPassword(TestConstants.PasswordTooShort)
            .Build();

        HttpResponseMessage response = await _client.PostAsJsonAsync(RegisterEndpoint, request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Password");
    }

    [Fact]
    public async Task RegisterUser_WithConsentFalse_Returns422()
    {
        RegisterUserRequest request = new RegisterUserRequestBuilder()
            .WithConsentGiven(false)
            .Build();

        HttpResponseMessage response = await _client.PostAsJsonAsync(RegisterEndpoint, request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RegisterUser_WithoutCorrelationId_ResponseIncludesCorrelationIdHeader()
    {
        RegisterUserRequest request = new RegisterUserRequestBuilder().Build();

        HttpResponseMessage response = await _client.PostAsJsonAsync(RegisterEndpoint, request);

        response.Headers.TryGetValues(TestConstants.CorrelationIdHeader, out IEnumerable<string>? values).Should().BeTrue();
        string? correlationId = values?.FirstOrDefault();
        correlationId.Should().NotBeNullOrEmpty();
    }
}

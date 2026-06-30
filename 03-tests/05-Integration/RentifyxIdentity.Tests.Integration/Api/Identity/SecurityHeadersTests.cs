using System.Net.Http.Json;
using FluentAssertions;
using RentifyxIdentity.Tests.Common.Builders;
using Xunit;

namespace RentifyxIdentity.Tests.Integration.Api.Identity;

[Collection("Integration")]
public sealed class SecurityHeadersTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private const string RegisterEndpoint = "/api/v1/auth/register";

    private async Task<HttpResponseMessage> AnyResponseAsync()
    {
        return await _client.PostAsJsonAsync(
            RegisterEndpoint,
            new RegisterUserRequestBuilder().Build());
    }

    [Fact]
    public async Task Response_HasContentSecurityPolicyHeader()
    {
        HttpResponseMessage response = await AnyResponseAsync();

        response.Headers.TryGetValues("Content-Security-Policy", out IEnumerable<string>? values)
            .Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be("default-src 'self'");
    }

    [Fact]
    public async Task Response_HasXFrameOptionsHeader()
    {
        HttpResponseMessage response = await AnyResponseAsync();

        response.Headers.TryGetValues("X-Frame-Options", out IEnumerable<string>? values)
            .Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be("DENY");
    }

    [Fact]
    public async Task Response_HasXContentTypeOptionsHeader()
    {
        HttpResponseMessage response = await AnyResponseAsync();

        response.Headers.TryGetValues("X-Content-Type-Options", out IEnumerable<string>? values)
            .Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be("nosniff");
    }

    [Fact]
    public async Task Response_HasReferrerPolicyHeader()
    {
        HttpResponseMessage response = await AnyResponseAsync();

        response.Headers.TryGetValues("Referrer-Policy", out IEnumerable<string>? values)
            .Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task Response_HasPermissionsPolicyHeader()
    {
        HttpResponseMessage response = await AnyResponseAsync();

        response.Headers.TryGetValues("Permissions-Policy", out IEnumerable<string>? values)
            .Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be("camera=(), microphone=(), geolocation=()");
    }
}

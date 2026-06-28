using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RentifyxIdentity.Tests.Integration.Auth;

/// <summary>
/// Bypasses JWT validation in integration tests.
/// Reads the user ID from <c>Authorization: Bearer {guid}</c> and sets
/// <c>ClaimTypes.NameIdentifier</c> on the resulting principal.
/// Replaced by Cognito JWT bearer authentication in E-04.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authHeader = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authHeader)
            || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or invalid Authorization header."));
        }

        string rawUserId = authHeader["Bearer ".Length..].Trim();

        if (!Guid.TryParse(rawUserId, out _))
            return Task.FromResult(AuthenticateResult.Fail("Bearer token is not a valid user ID."));

        Claim[] claims = [new Claim(ClaimTypes.NameIdentifier, rawUserId)];
        ClaimsIdentity identity = new(claims, SchemeName);
        AuthenticationTicket ticket = new(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity;
using RentifyxIdentity.Application.Features.Identity.Auth.VerifyEmail.Request;

namespace RentifyxIdentity.Api.Endpoints.Auth;

internal sealed class VerifyEmail : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/verify-email", HandleAsync)
           .WithName("VerifyEmail")
           .WithDescription("Verifies a user's email address using the token sent via email.")
           .WithTags(Tags.AUTH)
           .AllowAnonymous();
    }

    private static async Task<IResult> HandleAsync(
        VerifyEmailRequest request,
        IHandler<VerifyEmailRequest, UserResponse> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        ErrorOr<UserResponse> result = await handler.Handle(request, ct);

        return result.Match(
            response => Results.Ok(response),
            errors => errors.ToProblem(httpContext));
    }
}

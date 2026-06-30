using System.Security.Claims;
using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Identity.User.ExportData;
using RentifyxIdentity.Application.Features.Identity.User.ExportData.Request;

namespace RentifyxIdentity.Api.Endpoints.Users;

internal sealed class ExportData : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/users/me/data-export", HandleAsync)
           .WithName("ExportData")
           .WithDescription("Returns a full export of the authenticated user's personal data (LGPD Art. 18 IV).")
           .WithTags(Tags.USERS);
    }

    private static async Task<IResult> HandleAsync(
        IHandler<ExportDataRequest, UserDataExportResponse> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
            return Results.Unauthorized();

        ErrorOr<UserDataExportResponse> result = await handler.Handle(
            new ExportDataRequest(userId),
            ct);

        return result.Match(
            response => Results.Ok(response),
            errors => errors.ToProblem(httpContext));
    }
}

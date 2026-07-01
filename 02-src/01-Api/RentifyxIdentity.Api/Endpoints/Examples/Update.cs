using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Examples.Handlers.Update.Request;
using RentifyxIdentity.Application.Features.Examples.Mapper;
using RentifyxIdentity.Domain.Entities;

namespace RentifyxIdentity.Api.Endpoints.Examples;

internal sealed class Update : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/examples/{id:guid}", HandleAsync)
           .WithName("UpdateExample")
           .WithDescription("Update an existing example.")
           .WithTags(Tags.EXAMPLE);
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        UpdateExampleRequest request,
        IHandler<UpdateExampleRequest, ExampleEntity> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        ErrorOr<ExampleEntity> result = await handler.Handle(request with { Id = id }, ct);

        return result.Match(
            entity => Results.Ok(ExampleMapper.ToResponse(entity)),
            errors => errors.ToProblem(httpContext));
    }
}

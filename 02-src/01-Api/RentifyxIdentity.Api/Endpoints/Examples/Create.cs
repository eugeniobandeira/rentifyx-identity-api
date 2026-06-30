using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Examples.Handlers.Create.Request;
using RentifyxIdentity.Application.Features.Examples.Mapper;
using RentifyxIdentity.Domain.Entities;

namespace RentifyxIdentity.Api.Endpoints.Examples;

internal sealed class Create : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/examples", HandleAsync)
           .WithName("CreateExample")
           .WithDescription("Create a new example.")
           .WithTags(Tags.EXAMPLE);
    }

    private static async Task<IResult> HandleAsync(
        CreateExampleRequest request,
        IHandler<CreateExampleRequest, ExampleEntity> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        ErrorOr<ExampleEntity> result = await handler.Handle(request, ct);

        return result.Match(
            entity => Results.Created($"/api/v1/examples/{entity.Id}", ExampleMapper.ToResponse(entity)),
            errors => errors.ToProblem(httpContext));
    }
}

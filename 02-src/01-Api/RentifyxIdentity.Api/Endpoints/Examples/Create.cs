using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Examples.Handlers.Create.Request;
using RentifyxIdentity.Application.Features.Examples.Mapper;
using RentifyxIdentity.Domain.Entities;
using ErrorOr;

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
        CancellationToken cancellationToken = default)
    {
        ErrorOr<ExampleEntity> result = await handler.Handle(request, cancellationToken);

        return result.Match(
            entity => Results.Created($"/api/v1/examples/{entity.Id}", ExampleMapper.ToResponse(entity)),
            errors => errors.ToProblem(httpContext));
    }
}

using ErrorOr;
using RentifyxIdentity.Api.Abstract;
using RentifyxIdentity.Api.Extensions;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Common.Mapper;
using RentifyxIdentity.Application.Features.Examples.Handlers.GetAll.Request;
using RentifyxIdentity.Application.Features.Examples.Mapper;
using RentifyxIdentity.Domain.Common;
using RentifyxIdentity.Domain.Entities;

namespace RentifyxIdentity.Api.Endpoints.Examples;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/examples", HandleAsync)
           .WithName("GetAllExamples")
           .WithDescription("Get all active examples.")
           .WithTags(Tags.EXAMPLE);
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] GetAllExampleRequest request,
        IHandler<GetAllExampleRequest, PagedResult<ExampleEntity>> handler,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        ErrorOr<PagedResult<ExampleEntity>> result = await handler.Handle(request, ct);

        return result.Match(
            pagedResult => Results.Ok(ApiListResponseMapper.ToListResponse(
                [.. pagedResult.Items.Select(ExampleMapper.ToResponse)],
                pagedResult.Total,
                request.Page,
                request.PageSize)),
            errors => errors.ToProblem(httpContext));
    }
}

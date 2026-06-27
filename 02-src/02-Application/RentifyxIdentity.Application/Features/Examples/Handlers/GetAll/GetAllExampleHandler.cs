using ErrorOr;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Features.Examples.Handlers.GetAll.Request;
using RentifyxIdentity.Domain.Common;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Interfaces.Common;

namespace RentifyxIdentity.Application.Features.Examples.Handlers.GetAll;

public sealed class GetAllExampleHandler(
    IRepository<ExampleEntity, GetAllExampleRequest> repository,
    ILogger<GetAllExampleHandler> logger) : IHandler<GetAllExampleRequest, PagedResult<ExampleEntity>>
{
    public async Task<ErrorOr<PagedResult<ExampleEntity>>> Handle(GetAllExampleRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching examples. Payload={@Payload}", request);

        PagedResult<ExampleEntity> result = await repository.GetAllAsync(request, cancellationToken);

        logger.LogDebug("Fetched {Count} of {Total} examples.", result.Items.Count, result.Total);

        return result;
    }
}

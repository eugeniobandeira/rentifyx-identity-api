using ErrorOr;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Domain.Constants;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Interfaces.Common;

namespace RentifyxIdentity.Application.Features.Examples.Handlers.Delete;

public sealed class DeleteExampleHandler(
    IRepository<ExampleEntity> repository,
    ILogger<DeleteExampleHandler> logger) : IHandler<Guid, Deleted>
{
    public async Task<ErrorOr<Deleted>> Handle(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting example. Id={Id}", id);

        ExampleEntity? entity = await repository.GetByIdAsync(id, cancellationToken);

        if (entity is null)
        {
            logger.LogWarning("Example not found for deletion. Id={Id}", id);
            return Error.NotFound(ExampleErrorCodes.NotFound, $"Example {id} not found.");
        }

        await repository.DeleteAsync(entity, cancellationToken);

        logger.LogInformation("Example deleted successfully. Response={@Response}", entity);

        return Result.Deleted;
    }
}

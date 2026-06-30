using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using RentifyxIdentity.Application.Common.Handler;
using RentifyxIdentity.Application.Extensions;
using RentifyxIdentity.Application.Features.Examples.Handlers.Create.Request;
using RentifyxIdentity.Application.Features.Examples.Mapper;
using RentifyxIdentity.Domain.Entities;
using RentifyxIdentity.Domain.Interfaces.Common;

namespace RentifyxIdentity.Application.Features.Examples.Handlers.Create;

public sealed class CreateExampleHandler(
    IRepository<ExampleEntity> repository,
    IValidator<CreateExampleRequest> validator,
    ILogger<CreateExampleHandler> logger) : IHandler<CreateExampleRequest, ExampleEntity>
{
    public async Task<ErrorOr<ExampleEntity>> Handle(
        CreateExampleRequest request,
        CancellationToken ct = default)
    {
        logger.LogInformation("Creating example. Payload={@Payload}", request);

        List<Error>? errors = await validator.ValidateToErrorsAsync(request, ct);
        if (errors is not null)
            return errors;

        ExampleEntity entity = ExampleMapper.CreateExample(request);

        await repository.AddAsync(entity, ct);

        logger.LogInformation("Example created successfully. Response={@Response}", entity);

        return entity;
    }
}

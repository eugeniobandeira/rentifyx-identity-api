using RentifyxIdentity.Application.Features.Examples.Handlers.Create.Request;
using RentifyxIdentity.Application.Features.Examples.Handlers.Update.Request;
using RentifyxIdentity.Domain.Entities;

namespace RentifyxIdentity.Application.Features.Examples.Mapper;

public static class ExampleMapper
{
    public static ExampleEntity CreateExample(CreateExampleRequest request)
        => ExampleEntity.Create(request.Name, request.Description);

    public static void UpdateExample(ExampleEntity entity, UpdateExampleRequest request)
        => entity.Update(request.Name, request.Description);

    public static ExampleResponse ToResponse(ExampleEntity entity)
        => new(entity.Id, entity.Name, entity.Description, entity.IsActive, entity.CreatedAt, entity.UpdatedAt);
}

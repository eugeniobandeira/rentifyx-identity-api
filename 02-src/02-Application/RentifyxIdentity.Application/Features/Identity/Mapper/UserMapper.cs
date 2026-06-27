using RentifyxIdentity.Domain.Entities;

namespace RentifyxIdentity.Application.Features.Identity.Mapper;

public static class UserMapper
{
    public static UserResponse ToResponse(UserEntity entity)
        => new(
            entity.Id,
            entity.Email.ToString(),
            entity.Role.ToString(),
            entity.Status.ToString(),
            entity.CreatedAt
        );
}

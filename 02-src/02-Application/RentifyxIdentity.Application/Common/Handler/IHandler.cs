using ErrorOr;

namespace RentifyxIdentity.Application.Common.Handler;

public interface IHandler<TRequest, TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TRequest request, CancellationToken ct = default);
}

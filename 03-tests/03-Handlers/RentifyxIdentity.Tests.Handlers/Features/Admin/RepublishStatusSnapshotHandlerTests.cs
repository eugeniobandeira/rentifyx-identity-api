using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RentifyxIdentity.Application.Features.Admin.RepublishStatusSnapshot;
using RentifyxIdentity.Application.Features.Admin.RepublishStatusSnapshot.Request;
using RentifyxIdentity.Domain.Interfaces.Users;
using Xunit;

namespace RentifyxIdentity.Tests.Handlers.Features.Admin;

public sealed class RepublishStatusSnapshotHandlerTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<IUserStatusSnapshotPublisher> _publisherMock = new();
    private readonly Mock<ILogger<RepublishStatusSnapshotHandler>> _loggerMock = new();
    private readonly RepublishStatusSnapshotHandler _handler;

    public RepublishStatusSnapshotHandlerTests()
    {
        _handler = new RepublishStatusSnapshotHandler(
            _repositoryMock.Object,
            _publisherMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HappyPath_ActiveUsersExist_PublishesSnapshotAndReturnsCount()
    {
        List<Guid> activeUserIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        _repositoryMock
            .Setup(r => r.GetAllActiveUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUserIds);

        ErrorOr<RepublishStatusSnapshotResponse> result = await _handler.HandleAsync(
            new RepublishStatusSnapshotRequest());

        result.IsError.Should().BeFalse();
        result.Value.PublishedCount.Should().Be(3);

        _publisherMock.Verify(
            p => p.PublishSnapshotAsync(activeUserIds, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoActiveUsers_PublishesEmptyCollection_ReturnsZeroCount()
    {
        _repositoryMock
            .Setup(r => r.GetAllActiveUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        ErrorOr<RepublishStatusSnapshotResponse> result = await _handler.HandleAsync(
            new RepublishStatusSnapshotRequest());

        result.IsError.Should().BeFalse();
        result.Value.PublishedCount.Should().Be(0);

        _publisherMock.Verify(
            p => p.PublishSnapshotAsync(It.Is<IReadOnlyCollection<Guid>>(c => c.Count == 0), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

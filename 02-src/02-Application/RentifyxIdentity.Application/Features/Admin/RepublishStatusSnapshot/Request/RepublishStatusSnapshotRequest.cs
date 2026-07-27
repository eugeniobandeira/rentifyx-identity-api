using System.Diagnostics.CodeAnalysis;

namespace RentifyxIdentity.Application.Features.Admin.RepublishStatusSnapshot.Request;

/// <summary>No input - republishes a snapshot for every currently active user.</summary>
[SuppressMessage(
    "Major Code Smell",
    "S2094:Classes should not be empty",
    Justification = "IHandler<TRequest, TResponse> requires a request type even when the action takes no input.")]
public sealed record RepublishStatusSnapshotRequest;

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory;

/// <summary>Stores approval requests and terminal responses atomically for the current process lifetime.</summary>
/// <remarks>This adapter is explicitly ephemeral and makes no process-loss durability claim.</remarks>
public sealed class InMemoryApprovalStore: IApprovalStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ApprovalRequestId, Entry> _entries = [];

    /// <inheritdoc/>
    public ApprovalStoreCapabilities Capabilities { get; } = new(
        IsDurable: false,
        ProvidesTrustedControlPlane: true);

    /// <inheritdoc/>
    public ValueTask<ApprovalStoreCreateResult> CreateAsync(ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_entries.TryGetValue(request.Id, out var existing))
            {
                return ValueTask.FromResult(existing.Request == request
                    ? ApprovalStoreCreateResult.AlreadyExists
                    : ApprovalStoreCreateResult.Conflict);
            }

            _entries.Add(request.Id, new Entry(request, null));
            return ValueTask.FromResult(ApprovalStoreCreateResult.Created);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ApprovalStoreResolveResult> ResolveAsync(ApprovalResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_entries.TryGetValue(response.RequestId, out var existing))
            {
                return ValueTask.FromResult(ApprovalStoreResolveResult.NotFound);
            }

            if (existing.Response is { } terminal)
            {
                return ValueTask.FromResult(terminal == response
                    ? ApprovalStoreResolveResult.AlreadyResolved
                    : ApprovalStoreResolveResult.Conflict);
            }

            if (existing.Request.Binding != response.Binding)
            {
                return ValueTask.FromResult(ApprovalStoreResolveResult.Conflict);
            }

            _entries[response.RequestId] = existing with { Response = response };
            return ValueTask.FromResult(ApprovalStoreResolveResult.Resolved);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ApprovalStoreReadResult> ReadAsync(ApprovalRequestId requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId.Value, Guid.Empty);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return ValueTask.FromResult(_entries.TryGetValue(requestId, out var entry)
                ? new ApprovalStoreReadResult(entry.Request, entry.Response)
                : new ApprovalStoreReadResult(null, null));
        }
    }

    private sealed record Entry(ApprovalRequest Request, ApprovalResponse? Response);
}

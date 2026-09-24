// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.Collections.Frozen;

/// <summary>Retains one MCP catalog snapshot, invoker bindings, and the owning session lifetime.</summary>
internal sealed class McpToolProviderCapture: IToolProviderCapture
{
    private readonly FrozenDictionary<ToolIdentity, IToolInvoker> _invokers;
    private readonly IMcpClientSession _session;
    private readonly Lock _gate = new();
    private bool _closed;

    internal McpToolProviderCapture(
        ToolProviderSnapshot snapshot,
        IReadOnlyDictionary<ToolIdentity, IToolInvoker> invokers,
        IMcpClientSession session,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(invokers);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Snapshot = snapshot;
        _invokers = invokers.ToFrozenDictionary();
        _session = session;
    }

    /// <inheritdoc/>
    public ToolProviderSnapshot Snapshot { get; }

    /// <inheritdoc/>
    public ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(
        ToolIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(identity, default);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_closed)
            {
                return ValueTask.FromResult<ToolInvokerLeaseResult>(
                    new ToolInvokerUnavailable(identity, "The MCP provider capture is closed."));
            }
        }

        if (!_invokers.TryGetValue(identity, out var invoker))
        {
            return ValueTask.FromResult<ToolInvokerLeaseResult>(
                new ToolInvokerUnavailable(identity, "The MCP tool identity is not in the captured catalog."));
        }

        var descriptor = Snapshot.Tools.First(tool => tool.Id == identity.Id && tool.Version == identity.Version);
        IToolInvokerLease lease = new McpToolInvokerLease(invoker, descriptor, Snapshot.SourceVersion);
        return ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(lease));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _closed = true;
        }

        await _session.DisposeAsync().ConfigureAwait(false);
    }
}

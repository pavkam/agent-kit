// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>
/// Restricts an <see cref="IToolCatalogCapture"/> to a per-run tool allow-list, so the run neither advertises nor can resolve
/// or invoke a tool outside the list.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Snapshot"/> is the inner snapshot intersected with the allow-list through
/// <see cref="ToolCatalogSnapshot.IntersectWith"/>, so provider aliases, execution policies, and descriptors of excluded
/// tools are absent: a model call naming an excluded tool resolves as an unknown tool before any authorization or invocation.
/// <see cref="AcquireInvokerAsync"/> additionally refuses every identity outside the restricted snapshot without touching the
/// inner capture, which keeps the restriction in force for callers that bypass alias resolution.
/// </para>
/// <para>
/// The wrapper owns the inner capture: disposing it disposes the inner capture exactly once, with the inner capture's own
/// shared-completion semantics. It adds no state beyond the immutable restricted snapshot and is safe for concurrent use.
/// </para>
/// </remarks>
public sealed class AllowListedToolCatalogCapture: IToolCatalogCapture
{
    private readonly IToolCatalogCapture _inner;
    private readonly ImmutableHashSet<ToolIdentity> _identities;

    /// <summary>Initializes a restriction over <paramref name="inner"/>.</summary>
    /// <param name="inner">The capture to restrict; the new instance takes ownership of its disposal.</param>
    /// <param name="allowedTools">The tool identifiers the run may see and call; empty exposes no tool.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="allowedTools"/> is the default array.</exception>
    public AllowListedToolCatalogCapture(IToolCatalogCapture inner, ImmutableArray<ToolId> allowedTools)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfDefault(allowedTools);

        _inner = inner;
        Snapshot = inner.Snapshot.IntersectWith(allowedTools);
        _identities = [.. Snapshot.Tools.Select(static tool => new ToolIdentity(tool.Id, tool.Version))];
    }

    /// <inheritdoc/>
    public ToolCatalogSnapshot Snapshot { get; }

    /// <inheritdoc/>
    public ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(ToolIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(identity, default);

        return _identities.Contains(identity)
            ? _inner.AcquireInvokerAsync(identity, cancellationToken)
            : ValueTask.FromResult<ToolInvokerLeaseResult>(
                new ToolInvokerUnavailable(identity, "The tool is outside this run's tool allow-list."));
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

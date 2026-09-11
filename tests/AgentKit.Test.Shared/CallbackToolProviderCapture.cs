// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Exposes typed callbacks for adversarial source-acquisition and lifetime tests.</summary>
/// <param name="snapshot">The retained publication, or deliberately invalid metadata for boundary tests.</param>
public sealed class CallbackToolProviderCapture(ToolProviderSnapshot snapshot): IToolProviderCapture
{
    private int _snapshotReads;
    private int _acquisitions;
    private int _disposals;
    /// <summary>Gets or sets the typed acquisition behavior.</summary>
    public Func<ToolIdentity, CancellationToken, ValueTask<ToolInvokerLeaseResult>> Acquire { get; set; } = static (identity, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerUnavailable(identity, "unavailable"));
    /// <summary>Gets or sets the optional release callback.</summary>
    public Func<ValueTask>? Release { get; set; }
    /// <summary>Gets or sets a controlled snapshot getter override.</summary>
    public Func<ToolProviderSnapshot>? ReadSnapshot { get; set; }
    /// <summary>Gets how often the publication was read.</summary>
    public int SnapshotReads => Volatile.Read(ref _snapshotReads);
    /// <summary>Gets how often exact acquisition was requested.</summary>
    public int Acquisitions => Volatile.Read(ref _acquisitions);
    /// <summary>Gets how often the owner was released.</summary>
    public int Disposals => Volatile.Read(ref _disposals);
    /// <inheritdoc/>
    public ToolProviderSnapshot Snapshot { get { _ = Interlocked.Increment(ref _snapshotReads); return ReadSnapshot is null ? snapshot : ReadSnapshot(); } }
    /// <inheritdoc/>
    public ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(ToolIdentity identity, CancellationToken cancellationToken = default)
    {
        _ = Interlocked.Increment(ref _acquisitions);
        return Acquire(identity, cancellationToken);
    }
    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Increment(ref _disposals);
        return Release?.Invoke() ?? ValueTask.CompletedTask;
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Records required session-access audit dispatches and can run one deterministic pre-return callback.</summary>
internal sealed class RecordingSecurityAuditDispatcher: ISecurityAuditDispatcher
{
    private int _calls;

    /// <summary>Gets the number of records presented to the dispatcher.</summary>
    /// <value>The thread-safe dispatch count.</value>
    internal int Calls => Volatile.Read(ref _calls);

    /// <summary>Gets or sets a callback invoked after recording and before returning accepted.</summary>
    /// <value>A deterministic cancellation hook, or null for ordinary delivery.</value>
    internal Action? BeforeAccepted { get; set; }

    /// <summary>Gets or sets the result returned after the optional deterministic callback.</summary>
    /// <value>The configured audit result; accepted by default.</value>
    internal SecurityAuditDispatchResult NextResult { get; set; } = new SecurityAuditAccepted();

    /// <inheritdoc/>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
        SecurityAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        _ = Interlocked.Increment(ref _calls);
        BeforeAccepted?.Invoke();
        return ValueTask.FromResult(NextResult);
    }
}

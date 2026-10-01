// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Is the default intent signal: it discards the hint, because no worker is composed to receive it.</summary>
/// <remarks>Durable intents remain discoverable through the goal store, so dropping the signal loses latency, never work.</remarks>
internal sealed class NoOpDelegationIntentSignal: IDelegationIntentSignal
{
    /// <inheritdoc/>
    public ValueTask SignalAsync(DelegationIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Wakes a host worker when a child-admission intent has committed.</summary>
/// <remarks>The signal is an optimization and the only discovery path for goal stores that cannot enumerate intents across sessions. It is never correctness: a dispatcher ignores a signal failure because the durable intent already exists, and a worker treats every signal as a hint to re-read durable state. The default implementation discards the signal. Implementations are thread-safe.</remarks>
public interface IDelegationIntentSignal
{
    /// <summary>Notifies the worker of one committed intent.</summary>
    /// <param name="intent">The committed intent.</param>
    /// <param name="cancellationToken">Cancels the notification; it does not withdraw the committed intent.</param>
    /// <returns>A task completed when the signal was accepted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="intent"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask SignalAsync(DelegationIntent intent, CancellationToken cancellationToken = default);
}

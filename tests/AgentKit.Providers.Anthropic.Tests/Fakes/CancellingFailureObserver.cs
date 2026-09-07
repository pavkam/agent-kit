// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests.Fakes;

/// <summary>Records terminal delivery and cancels the caller immediately before throwing from failure publication.</summary>
/// <param name="callerCancellation">The caller cancellation source activated before the observer throws.</param>
internal sealed class CancellingFailureObserver(CancellationTokenSource callerCancellation): IModelResponseObserver
{
    /// <summary>Gets the number of terminal response events presented to this observer.</summary>
    public int TerminalEventCount { get; private set; }

    /// <inheritdoc/>
    public ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default)
    {
        if (responseEvent is ModelResponseFailed or ModelResponseCancelled or ModelResponseCompleted)
        {
            TerminalEventCount++;
        }

        if (responseEvent is not ModelResponseFailed)
        {
            return ValueTask.CompletedTask;
        }

        callerCancellation.Cancel();
        return ValueTask.FromException(new OperationCanceledException(callerCancellation.Token));
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates one <see cref="IHookDispatcher"/> subject and kernel-dispatch scenarios for conformance.</summary>
public interface IHookDispatcherConformanceFixture
{
    /// <summary>Gets the dispatcher under test.</summary>
    public IHookDispatcher Dispatcher { get; }

    /// <summary>Runs three registered hooks and returns the invocation order they observed.</summary>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>The hook identities in invocation order.</returns>
    public ValueTask<IReadOnlyList<HookRegistrationId>> DispatchOrderedMutatingHooksAsync(CancellationToken cancellationToken);

    /// <summary>Runs three registered hooks and returns the distinct invocation identities minted for them.</summary>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>One invocation identity per hook execution.</returns>
    public ValueTask<IReadOnlyList<HookInvocationId>> DispatchAndCollectInvocationIdsAsync(CancellationToken cancellationToken);

    /// <summary>Verifies point, dispatch, and event-argument identity mismatch fails before any hook runs.</summary>
    /// <param name="cancellationToken">Unused; present for uniform fixture shape.</param>
    /// <returns>A task that completes when the mismatch has been observed.</returns>
    public ValueTask AssertPointMismatchFailsBeforeDispatchAsync(CancellationToken cancellationToken);

    /// <summary>Verifies an isolated observational hook cannot leak a mutation after it throws.</summary>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>The payload observed after dispatch completes.</returns>
    public ValueTask<string?> DispatchIsolatedObserverAsync(CancellationToken cancellationToken);
}

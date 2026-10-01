// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the dispatcher a goal profile selected.</summary>
public sealed record DelegationDispatcherSelected: DelegationDispatcherSelectionResult
{
    /// <summary>Initializes a selected result.</summary>
    /// <param name="key">The registration key of the selected dispatcher.</param>
    /// <param name="dispatcher">The borrowed dispatcher instance.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is null.</exception>
    public DelegationDispatcherSelected(DelegationDispatcherKey key, IDelegationDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(dispatcher);
        Key = key;
        Dispatcher = dispatcher;
    }

    /// <summary>Gets the registration key of the selected dispatcher.</summary>
    public DelegationDispatcherKey Key { get; }

    /// <summary>Gets the borrowed dispatcher instance.</summary>
    public IDelegationDispatcher Dispatcher { get; }
}

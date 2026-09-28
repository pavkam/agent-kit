// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the one backend descriptor selected for an operation.</summary>
public sealed record DurableBackendSelected: DurableBackendSelectionResult
{
    /// <summary>Initializes a successful backend selection.</summary>
    /// <param name="descriptor">The selected immutable descriptor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is null.</exception>
    public DurableBackendSelected(DurableBackendDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Descriptor = descriptor;
    }

    /// <summary>Gets the selected descriptor.</summary>
    public DurableBackendDescriptor Descriptor { get; }
}

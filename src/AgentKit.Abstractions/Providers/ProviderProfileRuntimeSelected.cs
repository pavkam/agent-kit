// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A binding resolved to a usable runtime lease.</summary>
public sealed record ProviderProfileRuntimeSelected: ProviderProfileRuntimeSelectionResult
{
    /// <summary>Initializes a successful selection.</summary>
    /// <param name="runtime">The lease the adapter owns through the attempt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runtime"/> is null.</exception>
    public ProviderProfileRuntimeSelected(IProviderProfileRuntimeLease runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        Runtime = runtime;
    }

    /// <summary>Gets the selected runtime lease.</summary>
    public IProviderProfileRuntimeLease Runtime { get; }
}

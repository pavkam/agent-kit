// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A binding that could not be resolved to a runtime lease.</summary>
public sealed record ProviderProfileRuntimeUnavailable: ProviderProfileRuntimeSelectionResult
{
    /// <summary>Initializes an unavailable selection.</summary>
    /// <param name="binding">The binding that could not be resolved.</param>
    /// <param name="failure">The normalized failure describing why selection failed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> or <paramref name="failure"/> is null.</exception>
    public ProviderProfileRuntimeUnavailable(ProviderOperationBinding binding, ProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(failure);
        Binding = binding;
        Failure = failure;
    }

    /// <summary>Gets the binding that failed.</summary>
    public ProviderOperationBinding Binding { get; }

    /// <summary>Gets the normalized failure.</summary>
    public ProviderFailure Failure { get; }
}

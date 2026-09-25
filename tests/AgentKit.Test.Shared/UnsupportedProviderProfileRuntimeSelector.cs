// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="IProviderProfileRuntimeSelector"/> test double that throws when selected.
/// </summary>
public sealed class UnsupportedProviderProfileRuntimeSelector: IProviderProfileRuntimeSelector
{
    /// <inheritdoc/>
    public ValueTask<ProviderProfileRuntimeSelectionResult> SelectAsync(
        ProviderOperationBinding binding,
        ProtectedSemanticOperationContext operation,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support profile runtime selection.");
}

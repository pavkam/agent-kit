// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves one captured operation binding to endpoint and credential runtime evidence.</summary>
/// <remarks>
/// Selection is pure configuration resolution: it performs no provider I/O and grants no authority. Credential
/// material is resolved later through the returned <see cref="IProviderCredentialSource"/>.
/// </remarks>
public interface IProviderProfileRuntimeSelector
{
    /// <summary>Selects runtime evidence for one binding.</summary>
    /// <param name="binding">The endpoint and credential profile references captured before the attempt.</param>
    /// <param name="operation">The protected semantic operation requesting the binding.</param>
    /// <param name="cancellationToken">Cancels selection.</param>
    /// <returns>A selected lease or an unavailable outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> or <paramref name="operation"/> is null.</exception>
    public ValueTask<ProviderProfileRuntimeSelectionResult> SelectAsync(
        ProviderOperationBinding binding,
        ProtectedSemanticOperationContext operation,
        CancellationToken cancellationToken = default);
}

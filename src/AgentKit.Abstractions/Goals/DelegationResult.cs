// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The abstract outcome of one delegation request.</summary>
/// <remarks>A result is untrusted agent-produced data until result-schema and evidence validation succeed. A rejection before child creation is <see cref="DelegationRejected"/> and cannot invent child identities; <see cref="DelegationChildResult"/> exists only after the child goal was durably created.</remarks>
public abstract record DelegationResult
{
    /// <summary>Initializes the shared result fields.</summary>
    /// <param name="id">The delegation identity the result answers.</param>
    /// <param name="extensions">The forward-compatible extension data.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    private protected DelegationResult(DelegationId id, ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentNullException.ThrowIfNull(extensions);
        Id = id;
        Extensions = extensions;
    }

    /// <summary>Gets the delegation identity the result answers.</summary>
    public DelegationId Id { get; }

    /// <summary>Gets the forward-compatible extension data.</summary>
    public ExtensionData Extensions { get; }
}

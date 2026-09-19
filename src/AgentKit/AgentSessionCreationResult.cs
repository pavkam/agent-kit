// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable base for the outcome of one <see cref="Agent.CreateSessionAsync"/> attempt.</summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are <see cref="AgentSessionCreated"/> and
/// <see cref="AgentSessionCreationFailed"/>. Its constructor is <see langword="private protected"/>, so no
/// assembly outside AgentKit can add a third kind and defeat exhaustive handling.
/// </remarks>
public abstract record AgentSessionCreationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionCreationResult"/> record. This constructor is
    /// <see langword="private protected"/> so only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected AgentSessionCreationResult()
    {
    }
}

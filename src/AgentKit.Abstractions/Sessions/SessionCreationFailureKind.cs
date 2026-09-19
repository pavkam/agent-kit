// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed category of one failed facade session-creation attempt.</summary>
public enum SessionCreationFailureKind
{
    /// <summary>The catalog no longer enables the requested agent: removed, disabled, or replaced since resolution.</summary>
    AgentUnavailable,

    /// <summary>Fresh authorization could not be captured for the creating identity.</summary>
    AuthorizationUnavailable,

    /// <summary>The selected <see cref="ISessionCoordinator"/> returned a result other than <see cref="SessionCreated"/>.</summary>
    StoreRejected,
}

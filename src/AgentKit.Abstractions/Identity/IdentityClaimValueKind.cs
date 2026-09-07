// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes how a bounded identity claim value is interpreted without exposing provider-specific objects.</summary>
public enum IdentityClaimValueKind
{
    /// <summary>The value is opaque text.</summary>
    Text,
    /// <summary>The value is a Boolean encoded as canonical lowercase text.</summary>
    Boolean,
    /// <summary>The value is an integer encoded with invariant culture.</summary>
    WholeNumber,
}

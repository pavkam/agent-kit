// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies admission failures that occur before durable acceptance.</summary>
public enum InputRejectionKind
{
    /// <summary>The payload violates configured bounds or schema.</summary>
    InvalidInput,
    /// <summary>The caller lacks authority to address the session.</summary>
    Unauthorized,
    /// <summary>The authenticated identity differs from authorization evidence.</summary>
    IdentityMismatch,
    /// <summary>The addressed session or lane does not exist.</summary>
    AddressNotFound,
    /// <summary>Optional optimistic concurrency evidence is stale.</summary>
    StaleVersion,
}

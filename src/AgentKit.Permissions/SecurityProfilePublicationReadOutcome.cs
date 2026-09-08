// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Classifies the bounded result of one exact security-profile publication lookup.</summary>
internal enum SecurityProfilePublicationReadOutcome
{
    /// <summary>The reader found the exact immutable publication.</summary>
    Found,

    /// <summary>The reader found no publication at the requested exact coordinates.</summary>
    Unavailable,

    /// <summary>The caller cancelled before the lookup completed.</summary>
    Cancelled,
}

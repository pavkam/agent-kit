// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the most sensitive data intentionally transmitted by one request.</summary>
public enum NetworkDataClassification
{
    /// <summary>Data approved for public disclosure.</summary>
    Public,
    /// <summary>Non-public application or workspace data.</summary>
    Internal,
    /// <summary>Confidential data requiring destination-specific authority.</summary>
    Confidential,
    /// <summary>Highly restricted data normally denied by default.</summary>
    Restricted,
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies artifact data for authorization, retention, and egress policy.</summary>
public enum ArtifactDataClassification
{
    /// <summary>Content is approved for public disclosure.</summary>
    Public,
    /// <summary>Content is limited to the owning organization or tenant.</summary>
    Internal,
    /// <summary>Content contains confidential project or user data.</summary>
    Confidential,
    /// <summary>Content requires the strictest configured handling.</summary>
    Restricted,
}

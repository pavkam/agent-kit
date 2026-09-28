// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Labels observation payload sensitivity for capture, retention, and redaction policy.</summary>
/// <remarks>This shared classification applies to observation content only; domain-specific classifications remain on their owning contracts until explicitly mapped.</remarks>
public enum DataClassification
{
    /// <summary>Safe for broad operational export when capture is enabled.</summary>
    Public,

    /// <summary>Internal operational content with routine access controls.</summary>
    Internal,

    /// <summary>Content requiring elevated access and stronger redaction defaults.</summary>
    Confidential,

    /// <summary>Highly restricted content that should remain omitted unless explicitly allowed.</summary>
    Restricted,
}

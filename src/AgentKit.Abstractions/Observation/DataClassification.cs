// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Labels the sensitivity of observed, remembered, or retrieved content for capture, retention, redaction, and exposure policy.</summary>
/// <remarks>
/// This is the generic classification shared by observation payloads and the memory, document, and retrieval contracts. Values are ordered from least to most restricted, so a configured ceiling admits every value less than or equal to it. Artifact metadata and references use it directly.
/// </remarks>
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

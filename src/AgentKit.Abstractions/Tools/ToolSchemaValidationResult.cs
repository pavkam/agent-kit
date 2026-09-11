// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Separates canonical instance acceptance from invalid data and exhausted local validation resources.</summary>
public enum ToolSchemaValidationResult
{
    /// <summary>The unchanged instance satisfies every assertion in the retained canonical schema.</summary>
    Valid = 1,
    /// <summary>The instance violates the schema or contains duplicate object members.</summary>
    Invalid = 0,
    /// <summary>The instance or its complete evaluation exceeds a byte, depth, node, or work bound; validity is unknown.</summary>
    ResourceLimitExceeded = 2,
}

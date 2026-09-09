// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the loss-aware transformations a projection policy permits while deriving a bounded result representation.</summary>
/// <remarks>Values are composable flags. A snapshot rejects bits outside this closed v1 set so future transformations cannot be silently authorized.</remarks>
[Flags]
public enum ToolResultProjectionTransformations
{
    /// <summary>Permits no transformation.</summary>
    None = 0,

    /// <summary>Permits removal or replacement of classified content through redaction.</summary>
    Redaction = 1,

    /// <summary>Permits canonical representation changes that retain the same portable meaning.</summary>
    Normalization = 2,

    /// <summary>Permits a bounded semantic summary in place of source detail.</summary>
    Summarization = 4,

    /// <summary>Permits removal of a bounded tail or other explicitly recorded content portion.</summary>
    Truncation = 8,

    /// <summary>Permits replacing retained content with a separately governed external reference.</summary>
    Externalization = 16,
}

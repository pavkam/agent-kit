// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Selects how expected text is compared with produced text.</summary>
public enum ExactTextComparison
{
    /// <summary>Ordinal, case-sensitive equality of the complete text.</summary>
    Ordinal = 0,

    /// <summary>Ordinal equality ignoring case.</summary>
    OrdinalIgnoreCase = 1,

    /// <summary>Ordinal, case-sensitive equality after trimming leading and trailing white space from both sides.</summary>
    TrimmedOrdinal = 2,
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The category of a structural or policy problem found by a compaction validator.</summary>
public enum CompactionValidationIssueKind
{
    /// <summary>The candidate is structurally malformed (for example, an empty summary).</summary>
    InvalidStructure,

    /// <summary>The candidate references source entries that are not present in the snapshot.</summary>
    MissingSource,

    /// <summary>The candidate's cut breaks a causal pairing, such as a tool call and its result.</summary>
    BrokenCausality,

    /// <summary>The candidate does not represent a measurable size reduction over its source.</summary>
    NonReducing,

    /// <summary>The candidate's checkpoint content exceeds a configured size ceiling.</summary>
    UnboundedContent
}

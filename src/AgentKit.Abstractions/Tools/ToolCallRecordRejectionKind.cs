// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies why an <see cref="IToolCallRecorder"/> could not make a record durable.</summary>
public enum ToolCallRecordRejectionKind
{
    /// <summary>The evidence conflicts with durable state, such as a terminal record whose accepted record is missing or incoherent.</summary>
    Conflict,

    /// <summary>The evidence cannot be represented by the recorder's durable schema.</summary>
    Invalid,

    /// <summary>The durable store or session capability could not commit the record.</summary>
    Unavailable,
}

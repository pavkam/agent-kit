// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One incremental JSON fragment of a tool call's arguments, keyed by the
/// call it belongs to so that parallel tool-call streams can be
/// distinguished.
/// </summary>
public sealed record ToolArgumentsContentDelta: ContentDelta
{
    /// <summary>Initializes a new instance of the <see cref="ToolArgumentsContentDelta"/> record.</summary>
    /// <param name="toolCallId">The identity of the tool call this fragment belongs to.</param>
    /// <param name="jsonFragment">The incremental raw JSON text fragment of the call arguments.</param>
    /// <exception cref="ArgumentNullException"><paramref name="jsonFragment"/> is null.</exception>
    public ToolArgumentsContentDelta(ToolCallId toolCallId, string jsonFragment)
    {
        ArgumentNullException.ThrowIfNull(jsonFragment);

        ToolCallId = toolCallId;
        JsonFragment = jsonFragment;
    }

    /// <summary>Gets the identity of the tool call this fragment belongs to.</summary>
    public ToolCallId ToolCallId { get; init; }

    /// <summary>Gets the incremental raw JSON text fragment of the call arguments.</summary>
    public string JsonFragment { get; init; }
}

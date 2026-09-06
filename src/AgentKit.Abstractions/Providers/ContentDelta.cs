// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for one incremental fragment of an in-progress
/// response part, carried by a streamed model-part-delta event.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="TextContentDelta"/>, <see cref="ReasoningContentDelta"/>, and
/// tool-arguments, structured-data, and provider-native content deltas. Its
/// constructor is <see langword="private protected"/>, so no assembly
/// outside AgentKit.Abstractions can add a sixth kind. A delta never
/// carries a complete <see cref="ContentPart"/> by itself; the
/// corresponding completion event carries the fully assembled part once
/// its stream of deltas is closed.
/// </remarks>
public abstract record ContentDelta
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDelta"/> record.
    /// This constructor is <see langword="private protected"/> so only the
    /// closed set of kinds declared in this assembly can extend the
    /// hierarchy.
    /// </summary>
    private protected ContentDelta()
    {
    }
}

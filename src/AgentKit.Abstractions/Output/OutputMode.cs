// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The way one <see cref="OutputDefinition"/> expects its terminal candidate to be produced.</summary>
/// <remarks>
/// The first-party <c>AgentKit.Output</c> processor currently implements
/// candidate extraction and validation for <see cref="Text"/>,
/// <see cref="NativeSchema"/>, and <see cref="Prompted"/>.
/// <see cref="SyntheticTool"/>, <see cref="Media"/>, and <see cref="Union"/>
/// are declared for forward compatibility with the full structured-output
/// architecture, which resolves them against the not-yet-implemented
/// application tool catalog, media pipeline, and multi-alternative
/// selection machinery respectively; selecting one of them today produces a
/// typed, documented rejection rather than an unsupported-mode crash.
/// </remarks>
public enum OutputMode
{
    /// <summary>Validated or unvalidated plain text.</summary>
    Text,

    /// <summary>A synthetic, internal-protocol tool call carrying the result.</summary>
    SyntheticTool,

    /// <summary>A provider-enforced native schema response.</summary>
    NativeSchema,

    /// <summary>Schema and formatting instructions sent as ordinary context, parsed and validated locally.</summary>
    Prompted,

    /// <summary>A typed image, audio, file, or provider-native media result.</summary>
    Media,

    /// <summary>One of several named schemas with unambiguous selection.</summary>
    Union
}

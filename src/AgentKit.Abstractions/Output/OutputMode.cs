// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The way one <see cref="OutputDefinition"/> expects its terminal candidate to be produced.</summary>
/// <remarks>
/// The first-party <c>AgentKit.Output</c> processor implements candidate
/// extraction and validation for every mode. Before a request is sent, the
/// processor compares the mode with the selected model's capabilities and
/// either downgrades it to <see cref="Prompted"/> (when
/// <c>AgentOutputOptions.AllowProviderModeDowngrade</c> permits) or rejects the
/// definition as a provider capability mismatch.
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

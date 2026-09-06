// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Distinguishes the role a <see cref="MediaReferencePart"/> plays in a
/// message.
/// </summary>
/// <remarks>
/// Providers and context assembly both need to know whether a given media
/// item was supplied to the model or produced by it, and whether a smaller
/// representation exists purely as a preview: <see cref="Input"/> media
/// counts against a request's input limits and provider-egress
/// authorization, <see cref="Output"/> media is model-generated content
/// subject to the same trust rules as any other model output, and
/// <see cref="Thumbnail"/> media is a lightweight companion to a full
/// item, never a substitute for it when the full content is actually
/// needed.
/// </remarks>
public enum MediaSemantics
{
    /// <summary>Media supplied as input to the model.</summary>
    Input,

    /// <summary>Media produced as model output.</summary>
    Output,

    /// <summary>A thumbnail or preview representation of other media.</summary>
    Thumbnail
}

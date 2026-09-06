// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Distinguishes where the bytes for a <see cref="MediaReference"/> come
/// from.
/// </summary>
/// <remarks>
/// This distinction matters for both storage size and security: only
/// <see cref="InlineBytes"/> media actually embeds content in the durable
/// message, while <see cref="Uri"/> and <see cref="FileReference"/> media
/// carry a pointer that must be resolved, and authorized, by the component
/// that needs the bytes. AgentKit never fetches a raw external URL or opens
/// a file merely because a model or a stored message references it; doing
/// so is a protected operation subject to the same network/file-system
/// authorization as any other host-access request.
/// </remarks>
public enum MediaSourceKind
{
    /// <summary>The media is embedded directly as inline bytes.</summary>
    InlineBytes,

    /// <summary>The media is addressed by a remote URI that must be resolved separately.</summary>
    Uri,

    /// <summary>The media is addressed by an authorized file reference that must be resolved separately.</summary>
    FileReference
}

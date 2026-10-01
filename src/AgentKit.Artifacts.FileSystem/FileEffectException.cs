// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

/// <summary>Reports that a protected file-system effect was refused, unavailable, or failed, so the artifact operation must report the store unavailable.</summary>
/// <remarks>The message is content-free: it never carries a host path, artifact content, or grant evidence.</remarks>
internal sealed class FileEffectException: IOException
{
    /// <summary>Initializes the exception with a content-free explanation.</summary>
    /// <param name="message">The safe explanation.</param>
    internal FileEffectException(string message)
        : base(message)
    {
    }
}

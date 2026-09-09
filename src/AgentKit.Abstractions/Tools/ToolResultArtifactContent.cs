// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains a committed artifact reference without reading or inlining artifact bytes.</summary>
public sealed record ToolResultArtifactContent: ToolResultContent
{
    /// <summary>Initializes referenced artifact terminal content.</summary>
    /// <param name="reference">The immutable committed artifact reference.</param>
    /// <param name="extensions">Compatible immutable field evidence.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public ToolResultArtifactContent(ArtifactReference reference, ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(extensions);
        Reference = reference;
        Extensions = extensions;
    }
    /// <summary>Gets the committed artifact reference.</summary><value>A nonnull immutable reference.</value>
    public ArtifactReference Reference { get; }
    /// <summary>Gets compatible field evidence.</summary><value>A nonnull immutable bag.</value>
    public ExtensionData Extensions { get; }
}

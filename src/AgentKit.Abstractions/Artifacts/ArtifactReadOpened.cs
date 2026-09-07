// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns a readable stream for one verified committed artifact version.</summary>
/// <remarks>The caller must dispose this result. Disposing it disposes the underlying stream exactly once.</remarks>
public sealed record ArtifactReadOpened: ArtifactReadResult, IAsyncDisposable
{
    /// <summary>Initializes an owned artifact stream.</summary>
    /// <param name="reference">The exact committed reference.</param>
    /// <param name="content">The readable stream transferred to this result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="content"/> is null or unreadable.</exception>
    public ArtifactReadOpened(ArtifactReference reference, Stream content)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNotReadable(content);
        Reference = reference;
        Content = content;
    }

    /// <summary>Gets the exact committed reference.</summary>
    public ArtifactReference Reference { get; }
    /// <summary>Gets the owned readable stream.</summary>
    public Stream Content { get; }

    /// <summary>Asynchronously disposes the owned stream.</summary>
    /// <returns>A task representing stream disposal.</returns>
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

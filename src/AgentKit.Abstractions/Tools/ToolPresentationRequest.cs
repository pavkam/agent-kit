// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests bounded presentation from optional exact captured descriptor evidence and one immutable source.</summary>
public sealed record ToolPresentationRequest
{
    /// <summary>Initializes a presentation request.</summary>
    /// <param name="descriptor">The exact captured descriptor, or null when unavailable and generic fallback is required.</param>
    /// <param name="source">The nonnull projected evidence to present.</param>
    /// <param name="bounds">The nonnull operation limits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="bounds"/> is null.</exception>
    public ToolPresentationRequest(ToolDescriptor? descriptor, ToolPresentationSource source, ToolPresentationBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(bounds);
        Descriptor = descriptor; Source = source; Bounds = bounds;
    }

    /// <summary>Gets exact descriptor evidence.</summary><value>The captured descriptor, or null for fallback-only presentation.</value>
    public ToolDescriptor? Descriptor { get; }
    /// <summary>Gets projected call or result evidence.</summary><value>A nonnull immutable source.</value>
    public ToolPresentationSource Source { get; }
    /// <summary>Gets explicit input and output limits.</summary><value>Positive immutable bounds.</value>
    public ToolPresentationBounds Bounds { get; }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Describes one remote MCP resource entry from a catalog snapshot.</summary>
/// <remarks>Remote names, URIs, and descriptions are untrusted input.</remarks>
public sealed record McpResourceDescriptor
{
    /// <summary>Initializes a resource descriptor.</summary>
    /// <param name="uri">The resource URI advertised by the server.</param>
    /// <param name="name">The optional display name.</param>
    /// <param name="description">The optional untrusted description.</param>
    /// <param name="mimeType">The optional MIME type hint.</param>
    /// <exception cref="ArgumentException"><paramref name="uri"/> is uninitialized.</exception>
    public McpResourceDescriptor(
        string uri,
        string? name = null,
        string? description = null,
        string? mimeType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri, nameof(uri));
        Uri = uri;
        Name = name;
        Description = description;
        MimeType = mimeType;
    }

    /// <summary>Gets the resource URI.</summary>
    public string Uri { get; }

    /// <summary>Gets the optional display name.</summary>
    public string? Name { get; }

    /// <summary>Gets the optional untrusted description.</summary>
    public string? Description { get; }

    /// <summary>Gets the optional MIME type hint.</summary>
    public string? MimeType { get; }
}

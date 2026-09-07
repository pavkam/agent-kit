// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

using System.Reflection;

/// <summary>Describes one reflected object-in/object-out MCP tool method.</summary>
public sealed record McpToolMethodDescriptor
{
    /// <summary>Initializes a reflected tool method descriptor.</summary>
    /// <param name="name">The protocol-facing tool name.</param>
    /// <param name="version">The tool contract version.</param>
    /// <param name="description">The catalog description.</param>
    /// <param name="method">The reflected CLR method.</param>
    /// <param name="requestParameterName">The JSON-RPC argument member receiving the request object.</param>
    /// <param name="requestType">The request object type.</param>
    /// <param name="responseType">The response object type after unwrapping the asynchronous return.</param>
    /// <param name="readOnly">Whether the tool is read-only.</param>
    /// <param name="idempotent">Whether the tool is idempotent.</param>
    /// <param name="openWorld">Whether the tool reaches an open world.</param>
    /// <param name="destructive">Whether the tool may be destructive.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/>, <paramref name="version"/>, <paramref name="description"/>, or <paramref name="requestParameterName"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="method"/>, <paramref name="requestType"/>, or <paramref name="responseType"/> is null.</exception>
    public McpToolMethodDescriptor(
        McpToolName name,
        ToolVersion version,
        string description,
        MethodInfo method,
        string requestParameterName,
        Type requestType,
        Type responseType,
        bool readOnly,
        bool idempotent,
        bool openWorld,
        bool destructive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name.Value, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(version.Value, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestParameterName);
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(responseType);

        Name = name;
        Version = version;
        Description = description;
        Method = method;
        RequestParameterName = requestParameterName;
        RequestType = requestType;
        ResponseType = responseType;
        ReadOnly = readOnly;
        Idempotent = idempotent;
        OpenWorld = openWorld;
        Destructive = destructive;
    }

    /// <summary>Gets the protocol-facing tool name.</summary>
    public McpToolName Name { get; }

    /// <summary>Gets the tool contract version.</summary>
    public ToolVersion Version { get; }

    /// <summary>Gets the catalog description.</summary>
    public string Description { get; }

    /// <summary>Gets the reflected CLR method.</summary>
    public MethodInfo Method { get; }

    /// <summary>Gets the JSON-RPC argument member receiving the request object.</summary>
    public string RequestParameterName { get; }

    /// <summary>Gets the request object type.</summary>
    public Type RequestType { get; }

    /// <summary>Gets the unwrapped response object type.</summary>
    public Type ResponseType { get; }

    /// <summary>Gets whether the tool is read-only.</summary>
    public bool ReadOnly { get; }

    /// <summary>Gets whether the tool is idempotent.</summary>
    public bool Idempotent { get; }

    /// <summary>Gets whether the tool reaches an open world.</summary>
    public bool OpenWorld { get; }

    /// <summary>Gets whether the tool may be destructive.</summary>
    public bool Destructive { get; }
}

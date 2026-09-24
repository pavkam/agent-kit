// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Describes one remote MCP prompt entry from a catalog snapshot.</summary>
/// <remarks>Remote names and descriptions are untrusted input.</remarks>
public sealed record McpPromptDescriptor
{
    /// <summary>Initializes a prompt descriptor.</summary>
    /// <param name="name">The protocol-facing prompt name.</param>
    /// <param name="description">The optional untrusted description.</param>
    /// <param name="argumentNames">Optional declared argument names in server order.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is uninitialized, or <paramref name="argumentNames"/> is default or contains an uninitialized name.
    /// </exception>
    public McpPromptDescriptor(
        string name,
        string? description = null,
        ImmutableArray<string>? argumentNames = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        if (argumentNames is { } names)
        {
            ArgumentException.ThrowIfDefault(names);
            foreach (var argumentName in names)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(argumentName, nameof(argumentNames));
            }

            ArgumentNames = names;
        }
        else
        {
            ArgumentNames = [];
        }

        Name = name;
        Description = description;
    }

    /// <summary>Gets the protocol-facing prompt name.</summary>
    public string Name { get; }

    /// <summary>Gets the optional untrusted description.</summary>
    public string? Description { get; }

    /// <summary>Gets declared argument names in server order.</summary>
    public ImmutableArray<string> ArgumentNames { get; }
}

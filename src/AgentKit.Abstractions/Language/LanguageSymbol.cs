// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Contains one bounded symbol identity and source location returned by a language service.</summary>
public sealed record LanguageSymbol
{
    /// <summary>Initializes one normalized symbol.</summary>
    /// <param name="name">The non-empty symbol name.</param>
    /// <param name="kind">The non-empty portable or provider kind.</param>
    /// <param name="containerName">The containing symbol name when available.</param>
    /// <param name="location">The observed source location.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="kind"/> is blank.</exception>
    public LanguageSymbol(string name, string kind, string? containerName, LanguageLocation location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Name = name;
        Kind = kind;
        ContainerName = containerName;
        Location = location;
    }

    /// <summary>Gets the symbol name.</summary>
    public string Name { get; }
    /// <summary>Gets the portable or provider symbol kind.</summary>
    public string Kind { get; }
    /// <summary>Gets the containing symbol name when available.</summary>
    public string? ContainerName { get; }
    /// <summary>Gets the observed source location.</summary>
    public LanguageLocation Location { get; }
}

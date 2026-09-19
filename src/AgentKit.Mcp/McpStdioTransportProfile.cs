// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Describes a stdio MCP endpoint's command without starting a process.</summary>
/// <remarks>
/// The command and arguments are untrusted configuration. The stdio transport
/// later resolves and starts them through the process boundary under a process
/// grant. Environment variables are not stored here, so this profile cannot
/// smuggle secrets into a catalog snapshot.
/// </remarks>
public sealed record McpStdioTransportProfile: McpTransportProfile
{
    /// <summary>Initializes a stdio transport profile.</summary>
    /// <param name="command">The non-empty executable name or path to resolve later.</param>
    /// <param name="arguments">The ordered argument list. Empty is valid; default and null entries are not.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="command"/> is null, empty, or whitespace, or <paramref name="arguments"/> is default or contains null.
    /// </exception>
    public McpStdioTransportProfile(string command, ImmutableArray<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfContainsNull(arguments);
        Command = command;
        Arguments = arguments;
    }

    /// <summary>Gets the executable name or path. It is not resolved here.</summary>
    public string Command { get; }

    /// <summary>Gets the ordered arguments. Equality compares the sequence, not the array instance.</summary>
    public ImmutableArray<string> Arguments { get; }

    /// <summary>Compares the command and the ordered argument sequence.</summary>
    /// <param name="other">The profile to compare with.</param>
    /// <returns><see langword="true"/> when both profiles start the same command with the same arguments.</returns>
    public bool Equals(McpStdioTransportProfile? other) =>
        other is not null
        && string.Equals(Command, other.Command, StringComparison.Ordinal)
        && Arguments.SequenceEqual(other.Arguments);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Command, StringComparer.Ordinal);
        foreach (var argument in Arguments)
        {
            hash.Add(argument, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}

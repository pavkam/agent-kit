// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
namespace AgentKit;

/// <summary>Associates one provider-visible alias with one exact tool identity.</summary>
/// <remarks>This local association performs no catalog lookup, source publication capture, or authority grant.</remarks>
public sealed record ToolAliasAssignment
{
    /// <summary>Creates one explicit alias mapping without inferring identity from alias text.</summary>
    /// <param name="alias">The nondefault exact provider-visible alias.</param>
    /// <param name="tool">The nondefault canonical ID/version pair assigned by publication.</param>
    /// <exception cref="ArgumentOutOfRangeException">An argument is default.</exception>
    public ToolAliasAssignment(ToolAlias alias, ToolIdentity tool)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(alias, default);
        ArgumentOutOfRangeException.ThrowIfEqual(tool, default);
        Alias = alias;
        Tool = tool;
    }
    /// <summary>Gets the exact advertised alias.</summary>
    /// <value>A nondefault provider-facing identity compared ordinally.</value>
    public ToolAlias Alias { get; }

    /// <summary>Gets the explicitly assigned canonical tool.</summary>
    /// <value>A nondefault exact tool ID/version pair; it is not reconstructed from the alias.</value>
    public ToolIdentity Tool { get; }
}

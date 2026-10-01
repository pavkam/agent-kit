// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounds the tools and data a delegated child may use.</summary>
/// <remarks>A child receives the intersection of its parent's authority and this scope. The scope can only narrow; delegation policy rejects a scope that names a tool or data scope outside the parent's ceiling, and the scope itself never grants permission.</remarks>
public sealed record DelegationScope
{
    /// <summary>Initializes a validated delegation scope.</summary>
    /// <param name="allowedTools">The tools the child may use; empty grants none.</param>
    /// <param name="dataScopes">The opaque data-scope names the child may touch; empty grants none.</param>
    /// <exception cref="ArgumentException">An array is default, a tool is default, or a data scope is blank.</exception>
    /// <exception cref="ArgumentNullException">A data scope is null.</exception>
    public DelegationScope(ImmutableArray<ToolId> allowedTools, ImmutableArray<string> dataScopes)
    {
        ArgumentException.ThrowIfDefault(allowedTools);
        ArgumentException.ThrowIfDefault(dataScopes);
        foreach (var tool in allowedTools)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tool.Value, nameof(allowedTools));
        }

        foreach (var scope in dataScopes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope, nameof(dataScopes));
        }

        AllowedTools = allowedTools;
        DataScopes = dataScopes;
    }

    /// <summary>Gets the tools the child may use.</summary>
    public ImmutableArray<ToolId> AllowedTools { get; }

    /// <summary>Gets the opaque data-scope names the child may touch.</summary>
    public ImmutableArray<string> DataScopes { get; }

    /// <inheritdoc/>
    public bool Equals(DelegationScope? other) =>
        other is not null && AllowedTools.SequenceEqual(other.AllowedTools) && DataScopes.SequenceEqual(other.DataScopes);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var tool in AllowedTools)
        {
            hash.Add(tool);
        }

        foreach (var scope in DataScopes)
        {
            hash.Add(scope);
        }

        return hash.ToHashCode();
    }
}

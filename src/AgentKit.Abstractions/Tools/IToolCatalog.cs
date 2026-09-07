// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Resolves a <see cref="ToolId"/> to the registered <see cref="ITool"/>
/// that implements it.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberately reduced stand-in for the fuller tool catalog
/// described by the tools architecture, which additionally resolves a
/// specific <see cref="ToolVersion"/>, tracks duplicate-name and
/// provider-precedence rules across multiple registered providers, and
/// supports per-run catalog snapshots. Until that exists, one
/// <see cref="ToolId"/> maps to exactly one registered <see cref="ITool"/>
/// in a catalog, and a duplicate registration is a composition error at
/// catalog construction time rather than a resolvable precedence decision.
/// </para>
/// <para>
/// Implementations must be safe to call concurrently; a catalog is
/// normally built once from every registered <see cref="ITool"/> at
/// composition time and never mutated afterward.
/// </para>
/// </remarks>
public interface IToolCatalog
{
    /// <summary>Gets every tool registered in this catalog, in registration order.</summary>
    public ImmutableArray<ToolDescriptor> Descriptors { get; }

    /// <summary>Attempts to resolve <paramref name="id"/> to its registered tool.</summary>
    /// <param name="id">The tool identity to resolve.</param>
    /// <param name="tool">The resolved tool, when found.</param>
    /// <returns><see langword="true"/> if a tool is registered for <paramref name="id"/>.</returns>
    public bool TryResolve(ToolId id, [NotNullWhen(true)] out ITool? tool);
}

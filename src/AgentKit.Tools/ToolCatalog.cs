// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The default <see cref="IToolCatalog"/>: an immutable, additively built
/// catalog resolving each registered <see cref="ITool"/> by its
/// <see cref="ToolDescriptor.Id"/>.
/// </summary>
/// <remarks>
/// This catalog is built once from the tools supplied at construction and
/// never mutated afterward, so it is safe to register as a singleton and
/// resolve concurrently. Registering two tools with the same
/// <see cref="ToolId"/> is a composition error detected eagerly at
/// construction, not a resolvable precedence decision.
/// </remarks>
public sealed class ToolCatalog: IToolCatalog
{
    private readonly ImmutableDictionary<ToolId, ITool> _tools;

    /// <summary>Initializes a new instance of the <see cref="ToolCatalog"/> class.</summary>
    /// <param name="tools">Every tool to register in this catalog.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tools"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Two tools in <paramref name="tools"/> declare the same <see cref="ToolId"/>.
    /// </exception>
    public ToolCatalog(IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var builder = ImmutableDictionary.CreateBuilder<ToolId, ITool>();
        var descriptors = ImmutableArray.CreateBuilder<ToolDescriptor>();

        foreach (var tool in tools)
        {
            if (!builder.TryAdd(tool.Descriptor.Id, tool))
            {
                throw new ArgumentException(
                    $"Duplicate tool id '{tool.Descriptor.Id}' registered in the catalog.", nameof(tools));
            }

            descriptors.Add(tool.Descriptor);
        }

        _tools = builder.ToImmutable();
        Descriptors = descriptors.ToImmutable();
    }

    /// <inheritdoc/>
    public ImmutableArray<ToolDescriptor> Descriptors { get; }

    /// <inheritdoc/>
    public bool TryResolve(ToolId id, [NotNullWhen(true)] out ITool? tool) => _tools.TryGetValue(id, out tool);
}

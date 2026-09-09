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
    private readonly ImmutableDictionary<ToolId, ToolDescriptor> _capturedDescriptors;

    /// <summary>Initializes a new instance of the <see cref="ToolCatalog"/> class.</summary>
    /// <param name="tools">Every tool to register in this catalog.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="tools"/>, an element, or an element's descriptor is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Two tools in <paramref name="tools"/> declare the same <see cref="ToolId"/>.
    /// </exception>
    public ToolCatalog(IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var builder = ImmutableDictionary.CreateBuilder<ToolId, ITool>();
        var capturedDescriptors = ImmutableDictionary.CreateBuilder<ToolId, ToolDescriptor>();
        var descriptors = ImmutableArray.CreateBuilder<ToolDescriptor>();

        foreach (var tool in tools)
        {
            ArgumentNullException.ThrowIfNull(tool, nameof(tools));
            var descriptor = tool.Descriptor;
            ArgumentNullException.ThrowIfNull(descriptor, nameof(tools));
            ArgumentException.ThrowIfNotEqual(builder.TryAdd(descriptor.Id, tool), true, nameof(tools));

            capturedDescriptors.Add(descriptor.Id, descriptor);
            descriptors.Add(descriptor);
        }

        _tools = builder.ToImmutable();
        _capturedDescriptors = capturedDescriptors.ToImmutable();
        Descriptors = descriptors.ToImmutable();
    }

    /// <inheritdoc/>
    public ImmutableArray<ToolDescriptor> Descriptors { get; }

    /// <inheritdoc/>
    public bool TryResolve(ToolId id, [NotNullWhen(true)] out ITool? tool) => _tools.TryGetValue(id, out tool);

    /// <summary>Resolves the registered tool and the exact descriptor captured with it at catalog construction.</summary>
    /// <param name="id">The canonical tool identity to resolve.</param>
    /// <param name="tool">The borrowed registered tool instance when found; the catalog does not own its lifetime.</param>
    /// <param name="descriptor">The same immutable descriptor instance published by <see cref="Descriptors"/> when found.</param>
    /// <returns>True when both captured values exist for <paramref name="id"/>; otherwise false.</returns>
    public bool TryResolve(
        ToolId id,
        [NotNullWhen(true)] out ITool? tool,
        [NotNullWhen(true)] out ToolDescriptor? descriptor)
    {
        if (_tools.TryGetValue(id, out tool))
        {
            descriptor = _capturedDescriptors[id];
            return true;
        }

        descriptor = null;
        return false;
    }
}

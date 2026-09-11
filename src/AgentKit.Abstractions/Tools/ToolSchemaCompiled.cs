// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains a successfully preflighted canonical schema and its concurrently callable validation handle.</summary>
public sealed record ToolSchemaCompiled: ToolSchemaCompilationResult
{
    /// <summary>Transfers an immutable, resource-free compiled handle to its caller.</summary>
    /// <param name="schema">The nonnull handle retaining the exact canonical schema, profile, and compilation limits.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
    /// <remarks>Consumers must check retained evidence against their requested schema and engine profile before model exposure.</remarks>
    public ToolSchemaCompiled(ICompiledToolSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        Schema = schema;
    }
    /// <summary>Gets the exact compiled validation handle without re-resolving current DI registrations.</summary>
    /// <value>A nonnull immutable handle with no disposal obligation or authority.</value>
    public ICompiledToolSchema Schema { get; }
}

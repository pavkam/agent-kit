// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Compiles canonical tool schemas under one explicit immutable local capability profile.</summary>
/// <remarks>Implementations are concurrently callable, perform no implicit I/O, use no mutable global registries, and return no partial handle on cancellation. Compilation never downgrades schemas for providers or applies defaults. Model translation remains a separate preflight boundary.</remarks>
public interface IToolSchemaEngine
{
    /// <summary>Gets the profile whose capabilities and version remain stable for the engine lifetime.</summary>
    /// <value>The immutable nonnull exact dialect and keyword evidence.</value>
    public ToolSchemaProfile Profile { get; }
    /// <summary>Preflights the complete bounded canonical schema before creating an immutable validation handle.</summary>
    /// <param name="schema">The nonnull owned canonical schema; all schema branches must be checked before success.</param>
    /// <param name="limits">The nonnull byte, depth, node, and total work bounds.</param>
    /// <param name="cancellationToken">Propagates cancellation before and during local processing.</param>
    /// <returns>A complete handle retaining exact schema/profile/limit evidence, or a classified configuration rejection.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels before ownership of a complete handle transfers.</exception>
    public ToolSchemaCompilationResult Compile(JsonSchema schema, ToolSchemaLimits limits, CancellationToken cancellationToken = default);
}

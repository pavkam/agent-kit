// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates already-parsed values against one retained, preflighted canonical tool schema.</summary>
/// <remarks>Handles are immutable, concurrently callable, and resource-free. They retain no run, authority, DI container, or mutable registry. Callers must bound raw arguments before parsing separately. Validation preserves caller data and never applies defaults, coerces types, performs I/O, or invokes a tool.</remarks>
public interface ICompiledToolSchema
{
    /// <summary>Gets the exact owned canonical schema accepted at compilation.</summary>
    /// <value>The nonnull immutable schema, including every accepted assertion and annotation.</value>
    public JsonSchema Schema { get; }
    /// <summary>Gets the exact engine profile captured at compilation.</summary>
    /// <value>The nonnull immutable profile; later DI replacement cannot redirect this handle.</value>
    public ToolSchemaProfile Profile { get; }
    /// <summary>Gets the explicit resource bounds under which the complete schema was accepted.</summary>
    /// <value>The nonnull immutable compilation limits.</value>
    public ToolSchemaLimits CompilationLimits { get; }
    /// <summary>Checks a complete parsed instance against retained canonical assertions under fresh local bounds.</summary>
    /// <param name="instance">An initialized JSON value whose owning document remains alive for this call.</param>
    /// <param name="limits">The nonnull instance byte, depth, node, and total evaluation-work bounds.</param>
    /// <param name="cancellationToken">Propagates cancellation throughout local evaluation.</param>
    /// <returns>Valid, invalid, or resource-limited; cancellation never becomes an invalid argument result.</returns>
    /// <exception cref="ArgumentException"><paramref name="instance"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">The instance's owning document is disposed.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels before a complete result is returned.</exception>
    public ToolSchemaValidationResult Validate(JsonElement instance, ToolSchemaLimits limits, CancellationToken cancellationToken = default);
}

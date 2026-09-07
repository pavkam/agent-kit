// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One complete, immutable request to invoke a resolved and authorized
/// tool.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. <see cref="Arguments"/> are exactly what the
/// model produced for the originating <see cref="ToolCallPart"/>; a tool is
/// responsible for parsing and validating them against its own declared
/// input shape before acting on them.
/// </remarks>
public sealed record ToolInvocationRequest
{
    /// <summary>Initializes a new instance of the <see cref="ToolInvocationRequest"/> record.</summary>
    /// <param name="context">The execution context for this invocation.</param>
    /// <param name="arguments">The raw, unvalidated call arguments as provided by the model.</param>
    /// <param name="requestedAt">The time this invocation was requested.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public ToolInvocationRequest(ToolExecutionContext context, JsonElement arguments, DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(context);

        Context = context;
        Arguments = arguments;
        RequestedAt = requestedAt;
    }

    /// <summary>Gets the execution context for this invocation.</summary>
    public ToolExecutionContext Context { get; init; }

    /// <summary>Gets the raw, unvalidated call arguments as provided by the model.</summary>
    public JsonElement Arguments { get; init; }

    /// <summary>Gets the time this invocation was requested.</summary>
    public DateTimeOffset RequestedAt { get; init; }
}

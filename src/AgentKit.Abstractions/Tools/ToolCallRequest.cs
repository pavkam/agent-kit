// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One complete, immutable request to resolve, authorize, and invoke a
/// tool by identity — the entry point to <see cref="IToolInvoker"/>,
/// upstream of <see cref="ToolInvocationRequest"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Unlike <see cref="ToolInvocationRequest"/>,
/// which a resolved <see cref="ITool"/> receives after resolution and
/// authorization already happened, this type is what a caller submits
/// knowing only which tool it wants called and with what arguments.
/// </remarks>
public sealed record ToolCallRequest
{
    /// <summary>Initializes a new instance of the <see cref="ToolCallRequest"/> record.</summary>
    /// <param name="toolId">The identity of the tool to call.</param>
    /// <param name="context">The execution context for this call.</param>
    /// <param name="arguments">The raw, unvalidated call arguments as provided by the model.</param>
    /// <param name="requestedAt">The time this call was requested.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public ToolCallRequest(ToolId toolId, ToolExecutionContext context, JsonElement arguments, DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(context);

        ToolId = toolId;
        Context = context;
        Arguments = arguments;
        RequestedAt = requestedAt;
    }

    /// <summary>Gets the identity of the tool to call.</summary>
    public ToolId ToolId { get; init; }

    /// <summary>Gets the execution context for this call.</summary>
    public ToolExecutionContext Context { get; init; }

    /// <summary>Gets the raw, unvalidated call arguments as provided by the model.</summary>
    public JsonElement Arguments { get; init; }

    /// <summary>Gets the time this call was requested.</summary>
    public DateTimeOffset RequestedAt { get; init; }
}

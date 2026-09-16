// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One complete, immutable request to resolve, authorize, and invoke a
/// tool by reference — the entry point to <see cref="IToolInvoker"/>,
/// upstream of <see cref="ToolInvocationRequest"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Unlike <see cref="ToolInvocationRequest"/>,
/// which a resolved <see cref="ITool"/> receives after resolution and
/// authorization already happened, this type is what a caller submits
/// knowing only which tool it wants called and with what arguments.
/// <see cref="Tool"/> is normally unresolved (its <see cref="ToolReference.Id"/>
/// is null) when it originates from a parsed model response, since a parser
/// never consults the tool catalog; <see cref="IToolInvoker"/> is responsible
/// for resolving <see cref="ToolReference.ProviderAlias"/> against the
/// registered catalog before invoking.
/// </remarks>
public sealed record ToolCallRequest
{
    /// <summary>Initializes a new instance of the <see cref="ToolCallRequest"/> record.</summary>
    /// <param name="tool">The tool to call, resolved or not.</param>
    /// <param name="context">The execution context for this call.</param>
    /// <param name="arguments">The raw, unvalidated call arguments as provided by the model.</param>
    /// <param name="requestedAt">The time this call was requested.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tool"/> or <paramref name="context"/> is null.</exception>
    public ToolCallRequest(ToolReference tool, ToolExecutionContext context, JsonElement arguments, DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(context);

        Tool = tool;
        Context = context;
        Arguments = arguments;
        RequestedAt = requestedAt;
    }

    /// <summary>Gets the tool to call, resolved or not.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ToolReference Tool
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the execution context for this call.</summary>
    public ToolExecutionContext Context { get; init; }

    /// <summary>Gets the raw, unvalidated call arguments as provided by the model.</summary>
    public JsonElement Arguments { get; init; }

    /// <summary>Gets the time this call was requested.</summary>
    public DateTimeOffset RequestedAt { get; init; }
}

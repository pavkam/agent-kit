// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>
/// An <see cref="IToolInvoker"/> test double that answers every call
/// through a caller-supplied handler and records every request it
/// received.
/// </summary>
internal sealed class FakeToolInvoker: IToolInvoker
{
    private readonly Func<ToolCallRequest, ResolvedToolInvocation> _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeToolInvoker"/> class that always resolves the requested
    /// reference against a synthetic catalog entry sharing its alias text, the way a real catalog resolves a
    /// registered tool.
    /// </summary>
    /// <param name="handler">Produces the invocation result for each received call.</param>
    public FakeToolInvoker(Func<ToolCallRequest, ToolInvocationResult> handler)
        : this(request =>
        {
            var resolvedTool = request.Tool.Id is null
                ? new ToolReference(request.Tool.ProviderAlias, new ToolId(request.Tool.ProviderAlias.Value), new ToolVersion("1"))
                : request.Tool;
            return new ResolvedToolInvocation(resolvedTool, ToolResultProjectionPolicyReference.Default, handler(request));
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeToolInvoker"/> class with full control over the resolved
    /// tool reference and projection policy the fake reports, for exercising an unresolved (unknown-tool) outcome.
    /// </summary>
    /// <param name="handler">Produces the complete resolved invocation for each received call.</param>
    public FakeToolInvoker(Func<ToolCallRequest, ResolvedToolInvocation> handler) => _handler = handler;

    /// <summary>Gets every request this fake received, in call order.</summary>
    public List<ToolCallRequest> ReceivedRequests { get; } = [];

    /// <inheritdoc/>
    public Task<ResolvedToolInvocation> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReceivedRequests.Add(request);
        return Task.FromResult(_handler(request));
    }
}

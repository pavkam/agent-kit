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
    private readonly Func<ToolCallRequest, ToolInvocationResult> _handler;

    /// <summary>Initializes a new instance of the <see cref="FakeToolInvoker"/> class.</summary>
    /// <param name="handler">Produces the result for each received call.</param>
    public FakeToolInvoker(Func<ToolCallRequest, ToolInvocationResult> handler) => _handler = handler;

    /// <summary>Gets every request this fake received, in call order.</summary>
    public List<ToolCallRequest> ReceivedRequests { get; } = [];

    /// <inheritdoc/>
    public Task<ToolInvocationResult> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReceivedRequests.Add(request);
        return Task.FromResult(_handler(request));
    }
}

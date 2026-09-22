// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Diagnostics;

/// <summary>Adapts one legacy <see cref="ITool"/> implementation to the spec-shaped <see cref="IToolInvoker"/> contract.</summary>
/// <remarks>
/// The bridge maps <see cref="ToolInvocationContext"/> into the legacy <see cref="ToolInvocationRequest"/> shape
/// until feature packages implement <see cref="IToolInvoker"/> natively (workstream 4 chunk C9).
/// </remarks>
internal sealed class ToolInvokerBridge(ITool tool): IToolInvoker
{
    /// <inheritdoc/>
    public async ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tool);

        var descriptor = tool.Descriptor;
        ArgumentException.ThrowIfNotEqual(descriptor.Id, context.Tool.Id, nameof(context));
        ArgumentException.ThrowIfNotEqual(descriptor.Version, context.ToolVersion, nameof(context));

        var grant = context.InvocationGrant;
        var authorization = grant.Authorization
            ?? throw new InvalidOperationException(
                "Tool invocations through the ITool bridge require grants that retain complete authorization evidence.");

        var executionContext = new ToolExecutionContext(
            context.AgentId,
            context.SessionId,
            context.CallId,
            grant.Scope.Correlation,
            grant.Identity,
            authorization,
            sessionProfile: null);

        var request = new ToolInvocationRequest(executionContext, context.Arguments, context.RequestedAt);

        try
        {
            return await tool.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.Assert(exception is not null, "Exception variable is assigned in the catch clause.");
            return new ToolInvocationResult(
                new ToolCallOutcome(
                    ToolCallOutcomeKind.Failed,
                    ToolTerminalStatus.InvocationFailed,
                    SideEffectCertainty.DefinitelyNotPerformed,
                    retryable: false,
                    $"Tool '{descriptor.Id}' threw an unhandled exception during invocation.",
                    ExtensionData.Empty),
                []);
        }
    }
}

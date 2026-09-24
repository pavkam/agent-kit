// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Invokes one remote MCP tool through an open session.</summary>
public sealed class McpToolInvoker(
    IMcpClientSession session,
    ToolDescriptor tool,
    IIdentifierGenerator<McpRequestId> requestIds,
    ProtectedSemanticOperationContext operation): IToolInvoker
{
    private readonly IMcpClientSession _session = session;
    private readonly ToolDescriptor _tool = tool;
    private readonly IIdentifierGenerator<McpRequestId> _requestIds = requestIds;
    private readonly ProtectedSemanticOperationContext _operation = operation;

    /// <inheritdoc/>
    public async ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        JsonDocument? arguments = null;
        if (context.Arguments.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            arguments = JsonDocument.Parse(context.Arguments.GetRawText());
        }

        var request = new McpToolsCallRequest(
            _requestIds.Create(),
            _operation,
            context.CallId,
            _tool.Name,
            arguments);
        var authorized = new AuthorizedMcpRequest(request, context.InvocationGrant);
        var response = await _session.InvokeAsync(authorized, cancellationToken).ConfigureAwait(false);
        return MapResponse(response);
    }

    private static ToolInvocationResult MapResponse(McpResponse response) =>
        response switch
        {
            McpResponseSucceeded succeeded => Success(succeeded.Result.RootElement.GetRawText()),
            McpResponseDenied denied => Failure(denied.SafeMessage, ToolTerminalStatus.Denied),
            McpResponseCancelled => Failure("MCP request cancelled.", ToolTerminalStatus.Cancelled),
            McpResponseProtocolFailed failed => Failure(failed.SafeMessage, ToolTerminalStatus.InvocationFailed),
            McpResponseUnsupportedCapability unsupported => Failure(unsupported.SafeMessage, ToolTerminalStatus.Unsupported),
            _ => Failure("Unsupported MCP response.", ToolTerminalStatus.InvocationFailed),
        };

    private static ToolInvocationResult Success(string text) =>
        new(
            new ToolCallOutcome(
                ToolCallOutcomeKind.Success,
                ToolTerminalStatus.Succeeded,
                SideEffectCertainty.Unknown,
                retryable: false,
                failureReason: null,
                ExtensionData.Empty),
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)]);

    private static ToolInvocationResult Failure(string message, ToolTerminalStatus status) =>
        new(
            new ToolCallOutcome(
                status.ToOutcomeKind(),
                status,
                SideEffectCertainty.DefinitelyNotPerformed,
                retryable: false,
                failureReason: message,
                ExtensionData.Empty),
            [new TextPart(message, TextSemantics.Plain, ExtensionData.Empty)]);
}

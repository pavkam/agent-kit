// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>
/// The default <see cref="IToolInvoker"/>: resolves a call against the
/// registered <see cref="IToolCatalog"/>, authorizes it through the
/// registered <see cref="IToolAuthorizer"/>, and invokes the resolved tool,
/// translating every reachable failure into an ordinary
/// <see cref="ToolInvocationResult"/>.
/// </summary>
/// <remarks>
/// This class never lets a tool's thrown exception propagate as a fault to
/// its caller: an unknown tool, a denied authorization, and an
/// unhandled exception from <see cref="ITool.InvokeAsync"/> all become a
/// <see cref="ToolInvocationResult"/> whose <see cref="ToolCallOutcome.Kind"/>
/// is <see cref="ToolCallOutcomeKind.Rejected"/> or
/// <see cref="ToolCallOutcomeKind.Failed"/> respectively. Cancellation is
/// the one exception allowed to propagate when the caller's token is actually
/// canceled, since that represents the caller itself abandoning the operation
/// rather than a terminal outcome for it. An implementation-thrown
/// <see cref="OperationCanceledException"/> without caller cancellation is an
/// ordinary failed invocation.
/// </remarks>
public sealed class DefaultToolInvoker: IToolInvoker
{
    private readonly IToolCatalog _catalog;
    private readonly IToolAuthorizer _authorizer;

    /// <summary>Initializes a new instance of the <see cref="DefaultToolInvoker"/> class.</summary>
    /// <param name="catalog">The catalog used to resolve a call's tool identity.</param>
    /// <param name="authorizer">The authorizer used to decide whether a resolved call may proceed.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="catalog"/> or <paramref name="authorizer"/> is null.
    /// </exception>
    public DefaultToolInvoker(IToolCatalog catalog, IToolAuthorizer authorizer)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(authorizer);

        _catalog = catalog;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_catalog.TryResolve(request.ToolId, out var tool))
        {
            return Rejected($"Tool '{request.ToolId}' is not registered.");
        }

        var authorization = await _authorizer.AuthorizeAsync(
            new ToolAuthorizationRequest(request.Context, tool.Descriptor), cancellationToken).ConfigureAwait(false);

        if (authorization is ToolAuthorizationDenied denied)
        {
            return Rejected(denied.SafeMessage);
        }

        var invocationRequest = new ToolInvocationRequest(request.Context, request.Arguments, request.RequestedAt);

        try
        {
            return await tool.InvokeAsync(invocationRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Failed($"Tool '{request.ToolId}' threw an unhandled exception during invocation.");
        }
    }

    private static ToolInvocationResult Rejected(string safeMessage) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Rejected, safeMessage, ExtensionData.Empty),
        []);

    private static ToolInvocationResult Failed(string safeMessage) => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Failed, safeMessage, ExtensionData.Empty),
        []);
}

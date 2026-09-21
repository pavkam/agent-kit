// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Routes one approval request across additive handlers and returns the first decisive inline outcome.</summary>
public sealed class DefaultApprovalHandlerDispatcher: IApprovalHandlerDispatcher
{
    private readonly IReadOnlyList<IApprovalHandler> _handlers;

    /// <summary>Initializes the dispatcher over the configured handler registrations.</summary>
    /// <param name="handlers">The additive handlers in deterministic registration order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handlers"/> is null.</exception>
    public DefaultApprovalHandlerDispatcher(IEnumerable<IApprovalHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = handlers.ToArray();
    }

    /// <inheritdoc/>
    public async ValueTask<ApprovalHandlerResult> TryResolveAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var handler in _handlers)
        {
            ApprovalHandlerResult result;
            try
            {
                result = await handler.TryResolveAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return new ApprovalHandlerUnavailable("An approval handler failed.");
            }

            if (result is ApprovalHandlerResponded)
            {
                return result;
            }
        }

        return new ApprovalHandlerUnavailable("No approval handler resolved the request.");
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Routes one approval request to the configured additive handlers and returns the first decisive outcome.</summary>
/// <remarks>The dispatcher owns deterministic routing across handlers; it does not evaluate policy or itself decide the approval.</remarks>
public interface IApprovalHandlerDispatcher
{
    /// <summary>Attempts to resolve an approval request inline through the configured handlers.</summary>
    /// <param name="request">The durable approval request.</param>
    /// <param name="cancellationToken">Cancels dispatch before it completes.</param>
    /// <returns>The first decisive handler outcome, or an unavailable result when no handler can answer inline.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<ApprovalHandlerResult> TryResolveAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default);
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Applies fully planned patch batches with exact preflight and honest partial-effect settlement.</summary>
public interface IWorkspacePatchApplier
{
    /// <summary>Gets the component audience for every entry grant.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Preflights and applies one source-ordered patch plan.</summary>
    /// <param name="request">The exact separately authorized entries.</param>
    /// <param name="cancellationToken">Cancels before the first target effect; later cancellation is settled explicitly.</param>
    /// <returns>The operation and per-entry settlement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or a required entry grant is null.</exception>
    /// <exception cref="ArgumentException">The entry collection or exact content bytes are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An entry identity or kind is invalid.</exception>
    /// <exception cref="OperationCanceledException">Cancellation is requested before any target effect commits.</exception>
    public ValueTask<WorkspacePatchResult> ApplyPatchAsync(
        WorkspacePatchRequest request,
        CancellationToken cancellationToken = default);
}

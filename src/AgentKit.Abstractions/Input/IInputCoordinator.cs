// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Coordinates validation, authorization, durable admission, and promotion through a selected input queue.</summary>
/// <remarks>The coordinator does not own session truth. Implementations must return success only after durable acceptance or atomic promotion is committed.</remarks>
public interface IInputCoordinator
{
    /// <summary>Authorizes and durably admits input idempotently.</summary>
    /// <param name="request">The complete addressed admission request.</param><param name="cancellationToken">Cancellation of the caller's wait.</param>
    /// <returns>The typed admission result without a fabricated run identity on failure.</returns>
    public ValueTask<InputAdmissionResult> AdmitAsync(InputAdmissionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Promotes eligible input within an already accepted active operation.</summary>
    /// <remarks>Run-start acceptance on an idle lane requires the session owner's separate atomic ownership-install transaction and must not fabricate active-operation evidence for this method.</remarks>
    /// <param name="request">The active-operation promotion request.</param><param name="cancellationToken">Cancellation of the caller's wait.</param>
    /// <returns>The committed promotion, typed conflict, or rejection.</returns>
    public ValueTask<InputPromotionResult> PromoteAsync(InputPromotionRequest request, CancellationToken cancellationToken = default);
}

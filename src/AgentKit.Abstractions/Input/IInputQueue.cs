// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines durable idempotent admission and atomic promotion over session-backed queue state.</summary>
/// <remarks>Implementations preserve lane isolation, admitted order, cutoff semantics, capacity, optimistic concurrency, and distributed fencing. This contract does not claim to install a new run operation on an idle lane.</remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Queue is the normative domain name for durable input admission and promotion.")]
public interface IInputQueue
{
    /// <summary>Appends one validated admission or returns its existing equivalent receipt.</summary>
    /// <param name="request">The authorized admission request.</param><param name="admissionId">The newly allocated acceptance identity.</param>
    /// <param name="effectiveInput">The captured effective payload.</param><param name="preprocessing">The preprocessing evidence used for replay equality.</param>
    /// <param name="admittedAt">The injected-clock timestamp.</param><param name="cancellationToken">Cancellation of the caller's wait.</param>
    /// <returns>The durable admission result.</returns>
    public ValueTask<InputAdmissionResult> AppendAsync(InputAdmissionRequest request, AdmissionId admissionId,
        AgentInput effectiveInput, InputPreprocessingManifest preprocessing, DateTimeOffset admittedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Plans, revalidates, and atomically commits eligible input for an already active operation.</summary>
    /// <param name="request">The exact active-operation request and cutoff.</param><param name="cancellationToken">Cancellation of the caller's wait.</param>
    /// <returns>The committed promotion or typed stale/rejected outcome.</returns>
    public ValueTask<InputPromotionResult> PromoteAsync(InputPromotionRequest request, CancellationToken cancellationToken = default);
}

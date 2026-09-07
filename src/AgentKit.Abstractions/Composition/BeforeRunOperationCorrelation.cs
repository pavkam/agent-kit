// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Correlates an operation, such as input admission or session creation,
/// that occurred before any run started and therefore has no
/// <see cref="RunId"/> to attach to.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share and compare across threads without
/// synchronization. It exists because meaningful causal work — accepting a
/// submission, issuing an admission receipt, deciding to queue input rather
/// than start a run immediately — happens before a run is created, and that
/// work still needs a stable way to say "this later run/decision was caused
/// by that earlier admission."
/// </remarks>
public sealed record BeforeRunOperationCorrelation: OperationCorrelation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BeforeRunOperationCorrelation"/>
    /// record.
    /// </summary>
    /// <param name="operationId">The stable identity of the causal operation.</param>
    /// <param name="admissionId">
    /// The admission receipt that caused this operation, when the operation
    /// resulted from input admission; <see langword="null"/> when it did
    /// not (for example, a standalone session-creation request).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationId"/> or a present <paramref name="admissionId"/> is default.</exception>
    public BeforeRunOperationCorrelation(OperationId operationId, AdmissionId? admissionId)
        : base(operationId) => AdmissionId = admissionId;

    /// <summary>
    /// Gets the admission receipt that caused this operation, when the
    /// operation resulted from input admission.
    /// </summary>
    /// <value>The nondefault causing admission identity, or <see langword="null"/> when admission did not cause the operation.</value>
    /// <exception cref="ArgumentOutOfRangeException">An init assignment supplies a present default value.</exception>
    public AdmissionId? AdmissionId
    {
        get;
        init
        {
            if (value is { } admissionId)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(admissionId.Value, Guid.Empty, "admissionId");
            }

            field = value;
        }
    }
}

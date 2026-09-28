// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Redacts explicitly classified observation payloads before they reach a sink.</summary>
/// <remarks>Implementations must fail closed: redaction failure returns <see cref="ContentOmitted"/>, never the original payload.</remarks>
public interface IObservationRedactor
{
    /// <summary>Redacts one observation payload under the supplied policy.</summary>
    /// <param name="content">The non-null classified content owned by the caller.</param>
    /// <param name="policy">The non-null effective policy for this export attempt.</param>
    /// <param name="cancellationToken">Cancels the redaction wait only; it does not rewrite an accepted omission decision.</param>
    /// <returns>A redacted payload or an omission result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> or <paramref name="policy"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The wait was cancelled.</exception>
    public ValueTask<RedactionResult> RedactAsync(
        ObservationContent content,
        ObservationPolicy policy,
        CancellationToken cancellationToken = default);
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Checks a produced compaction candidate for structural correctness and
/// policy compliance before it may be activated.
/// </summary>
/// <remarks>
/// A validator never trusts a strategy's output implicitly: every candidate
/// is checked against its source snapshot before activation, regardless of
/// which strategy or how deterministic it claims to be. Implementations
/// must be safe to call concurrently for independent requests and must not
/// mutate any input they receive.
/// </remarks>
public interface ICompactionValidator
{
    /// <summary>Validates one produced compaction candidate.</summary>
    /// <param name="request">The validation request.</param>
    /// <param name="cancellationToken">A token used to cancel validation.</param>
    /// <returns>
    /// A task that resolves to the closed validation outcome: a validated
    /// candidate, a rejection with specific issues, or an unexpected
    /// failure.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<CompactionValidationResult> ValidateAsync(
        CompactionValidationRequest request, CancellationToken cancellationToken = default);
}

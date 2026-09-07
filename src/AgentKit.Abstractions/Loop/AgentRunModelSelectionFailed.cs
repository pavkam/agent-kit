// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The run ended because no usable model could be chosen for it, before any
/// provider was contacted.
/// </summary>
/// <remarks>
/// <para>
/// This outcome is deliberately distinct from
/// <see cref="AgentRunFailed"/>. A provider failure means a provider was
/// reached and something went wrong; this means the run never got that far,
/// because the configured policy, catalog, and registered adapters could not
/// produce an executable model.
/// </para>
/// <para>
/// It covers an unusable policy, a policy whose candidates are absent or
/// incompatible, and a model that is configured in the catalog but has no
/// registered adapter to execute it. The diagnostics distinguish those cases.
/// </para>
/// </remarks>
public sealed record AgentRunModelSelectionFailed: AgentRunOutcome
{
    private readonly string _safeReason;
    private readonly ImmutableArray<ModelSelectionDiagnostic> _diagnostics;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AgentRunModelSelectionFailed"/> record.
    /// </summary>
    /// <param name="safeReason">
    /// A redacted, human-readable explanation of why no model could be used.
    /// </param>
    /// <param name="diagnostics">
    /// Per-candidate selection outcomes when selection ran. An empty array is
    /// valid when the policy itself was rejected before any candidate was
    /// examined.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeReason"/> is null, empty, or whitespace-only, or
    /// <paramref name="diagnostics"/> is uninitialized or contains
    /// <see langword="null"/>.
    /// </exception>
    public AgentRunModelSelectionFailed(
        string safeReason,
        ImmutableArray<ModelSelectionDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        ArgumentException.ThrowIfContainsNull(diagnostics);

        _safeReason = safeReason;
        _diagnostics = diagnostics;
    }

    /// <summary>Gets the redacted explanation of why no model could be used.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string SafeReason
    {
        get => _safeReason;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(SafeReason));
            _safeReason = value;
        }
    }

    /// <summary>Gets the per-candidate selection outcomes.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized array or one
    /// containing <see langword="null"/>.
    /// </exception>
    public ImmutableArray<ModelSelectionDiagnostic> Diagnostics
    {
        get => _diagnostics;
        init
        {
            ArgumentException.ThrowIfContainsNull(value, nameof(Diagnostics));
            _diagnostics = value;
        }
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The chosen model together with the evidence that explains and reproduces
/// the choice.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// The catalog version is part of the decision because a model choice is only
/// reproducible against the catalog it was made from. Retaining it lets a
/// later investigation distinguish "the policy changed" from "the catalog
/// changed".
/// </para>
/// </remarks>
public sealed record ModelSelectionDecision
{
    private readonly ModelDescriptor _model;
    private readonly string _reason;
    private readonly ImmutableArray<ModelSelectionDiagnostic> _diagnostics;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ModelSelectionDecision"/> record.
    /// </summary>
    /// <param name="model">The chosen descriptor.</param>
    /// <param name="catalogVersion">
    /// The catalog revision the choice was made against.
    /// </param>
    /// <param name="reason">
    /// A redacted, human-readable explanation of why this model was chosen.
    /// </param>
    /// <param name="diagnostics">
    /// Per-candidate outcomes, including candidates skipped before this one.
    /// An empty array is valid when the first candidate matched immediately.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="model"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is null, empty, or whitespace-only, or
    /// <paramref name="diagnostics"/> is uninitialized or contains
    /// <see langword="null"/>.
    /// </exception>
    public ModelSelectionDecision(
        ModelDescriptor model,
        ModelCatalogVersion catalogVersion,
        string reason,
        ImmutableArray<ModelSelectionDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfContainsNull(diagnostics);

        _model = model;
        CatalogVersion = catalogVersion;
        _reason = reason;
        _diagnostics = diagnostics;
    }

    /// <summary>Gets the chosen descriptor.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ModelDescriptor Model
    {
        get => _model;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Model));
            _model = value;
        }
    }

    /// <summary>Gets the catalog revision the choice was made against.</summary>
    public ModelCatalogVersion CatalogVersion { get; init; }

    /// <summary>Gets the redacted explanation of the choice.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string Reason
    {
        get => _reason;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Reason));
            _reason = value;
        }
    }

    /// <summary>Gets the per-candidate outcomes.</summary>
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

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The policy was valid but no candidate could satisfy the request's
/// requirements.
/// </summary>
/// <remarks>
/// This is a normal typed outcome, produced before any credential resolution,
/// hook, or network access. The per-candidate diagnostics are the actionable
/// part: they distinguish a misspelled alias from a genuine capability gap.
/// </remarks>
public sealed record NoCompatibleModel: ModelSelectionResult
{
    private readonly ModelRequirements _requirements;
    private readonly ImmutableArray<ModelSelectionDiagnostic> _diagnostics;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoCompatibleModel"/>
    /// record.
    /// </summary>
    /// <param name="requirements">The requirements that could not be met.</param>
    /// <param name="diagnostics">
    /// Why each candidate was rejected. At least one diagnostic is required,
    /// because an unexplained selection failure is not actionable.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="requirements"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="diagnostics"/> is uninitialized, empty, or contains
    /// <see langword="null"/>.
    /// </exception>
    public NoCompatibleModel(
        ModelRequirements requirements,
        ImmutableArray<ModelSelectionDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentException.ThrowIfDefaultOrEmpty(diagnostics);
        ArgumentException.ThrowIfContainsNull(diagnostics);

        _requirements = requirements;
        _diagnostics = diagnostics;
    }

    /// <summary>Gets the requirements that could not be met.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ModelRequirements Requirements
    {
        get => _requirements;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Requirements));
            _requirements = value;
        }
    }

    /// <summary>Gets why each candidate was rejected.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized, empty, or
    /// null-containing array.
    /// </exception>
    public ImmutableArray<ModelSelectionDiagnostic> Diagnostics
    {
        get => _diagnostics;
        init
        {
            ArgumentException.ThrowIfDefaultOrEmpty(value, nameof(Diagnostics));
            ArgumentException.ThrowIfContainsNull(value, nameof(Diagnostics));
            _diagnostics = value;
        }
    }
}

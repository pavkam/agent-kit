// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The agent exists but its definition cannot be used with the current composition.</summary>
/// <remarks>
/// This is distinct from <see cref="AgentNotFound"/>: the identity is real, but something it selects is missing,
/// ambiguous, or incompatible. It is a configuration error to fix rather than a client mistake, and the
/// diagnostics say which part is wrong.
/// </remarks>
public sealed record InvalidAgent: AgentResolution
{
    private readonly ImmutableArray<CompositionDiagnostic> _diagnostics;

    /// <summary>Initializes a new instance of the <see cref="InvalidAgent"/> record.</summary>
    /// <param name="agentId">The agent whose definition is unusable.</param>
    /// <param name="diagnostics">
    /// Why the definition is unusable. At least one diagnostic is required, because an unexplained invalid
    /// definition is not actionable.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="diagnostics"/> is uninitialized, empty, or contains <see langword="null"/>.
    /// </exception>
    public InvalidAgent(AgentId agentId, ImmutableArray<CompositionDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(diagnostics);
        ArgumentException.ThrowIfContainsNull(diagnostics);

        AgentId = agentId;
        _diagnostics = diagnostics;
    }

    /// <summary>Gets the agent whose definition is unusable.</summary>
    public AgentId AgentId { get; init; }

    /// <summary>Gets why the definition is unusable.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized, empty, or null-containing array.
    /// </exception>
    public ImmutableArray<CompositionDiagnostic> Diagnostics
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

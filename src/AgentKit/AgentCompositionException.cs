// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Thrown when a composition cannot produce a runnable engine.
/// </summary>
/// <remarks>
/// <para>
/// This exception reports configuration mistakes discovered at build time —
/// a missing engine-wide service, no published agent definition, or a
/// definition whose collaborators cannot be resolved. It is deliberately
/// thrown during <see cref="AgentEngineBuilder.Build"/> rather than at the
/// first run, so a misconfigured process fails at startup instead of when a
/// user is waiting.
/// </para>
/// <para>
/// <see cref="Diagnostics"/> carries every problem found, not just the first,
/// so an operator can fix a broken composition in one pass.
/// </para>
/// </remarks>
public sealed class AgentCompositionException: Exception
{
    /// <summary>
    /// Initializes an exception carrying every composition problem found.
    /// </summary>
    /// <param name="diagnostics">
    /// The problems found. At least one is required.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="diagnostics"/> is uninitialized, empty, or contains
    /// <see langword="null"/>.
    /// </exception>
    public AgentCompositionException(ImmutableArray<CompositionDiagnostic> diagnostics)
        : base(BuildMessage(diagnostics)) => Diagnostics = diagnostics;

    /// <summary>
    /// Initializes an exception with a single message and no structured
    /// diagnostics.
    /// </summary>
    /// <param name="message">The failure description.</param>
    public AgentCompositionException(string message)
        : base(message) => Diagnostics = [];

    /// <summary>
    /// Initializes an exception with a message and an underlying cause.
    /// </summary>
    /// <param name="message">The failure description.</param>
    /// <param name="innerException">The underlying cause.</param>
    public AgentCompositionException(string message, Exception innerException)
        : base(message, innerException) => Diagnostics = [];

    /// <summary>Initializes an exception with no description.</summary>
    /// <remarks>
    /// Present to satisfy the standard exception constructor pattern.
    /// Prefer the overload taking diagnostics so the failure is actionable.
    /// </remarks>
    public AgentCompositionException()
        : base("The AgentKit composition is not runnable.") => Diagnostics = [];

    /// <summary>Gets every composition problem found.</summary>
    /// <value>
    /// Possibly empty when the exception was created from a plain message.
    /// </value>
    public ImmutableArray<CompositionDiagnostic> Diagnostics { get; }

    private static string BuildMessage(ImmutableArray<CompositionDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(diagnostics);
        ArgumentException.ThrowIfContainsNull(diagnostics);

        return "The AgentKit composition is not runnable: "
            + string.Join("; ", diagnostics);
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Supplies the output processor's evidence that a bounded repair attempt is required.</summary>
/// <remarks>The typed decision is preserved for revalidation. This cause neither performs a provider request nor consumes an output-repair budget.</remarks>
public sealed record OutputRepairContinuationCause: RunContinuationCause
{
    /// <summary>Initializes evidence for an output-repair continuation.</summary>
    /// <param name="decision">The non-null typed processor decision requesting the bounded repair attempt.</param>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null.</exception>
    public OutputRepairContinuationCause(OutputRetryRequired decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        Decision = decision;
    }

    /// <summary>Gets the typed output processor decision requesting repair.</summary>
    /// <value>A non-null immutable decision that remains subject to session-owner revalidation and budget checks.</value>
    public OutputRetryRequired Decision { get; }
}

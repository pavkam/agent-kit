// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The portable terminal disposition of one tool call, carried by its
/// <see cref="ToolResultPart"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Every
/// accepted tool call reaches exactly one terminal result, and this
/// outcome's <see cref="Kind"/> is what makes that terminal state
/// observable to the model and to durable records without requiring either
/// to parse free-form result content to infer success or failure.
/// </remarks>
public sealed record ToolCallOutcome
{
    /// <summary>Initializes a new instance of the <see cref="ToolCallOutcome"/> record.</summary>
    /// <param name="kind">The terminal disposition category.</param>
    /// <param name="failureReason">
    /// A safe, non-sensitive description of the failure, when
    /// <paramref name="kind"/> is not <see cref="ToolCallOutcomeKind.Success"/>.
    /// This text may be surfaced to the model, so it must never include
    /// secrets, credentials, or internal diagnostic detail.
    /// </param>
    /// <param name="extensions">Provider- or tool-specific outcome data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ToolCallOutcome(
        ToolCallOutcomeKind kind,
        string? failureReason,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        Kind = kind;
        FailureReason = failureReason;
        Extensions = extensions;
    }

    /// <summary>Gets the terminal disposition category.</summary>
    public ToolCallOutcomeKind Kind { get; init; }

    /// <summary>
    /// Gets a safe, non-sensitive description of the failure, when
    /// <see cref="Kind"/> is not <see cref="ToolCallOutcomeKind.Success"/>.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>Gets provider- or tool-specific outcome data.</summary>
    public ExtensionData Extensions { get; init; }
}

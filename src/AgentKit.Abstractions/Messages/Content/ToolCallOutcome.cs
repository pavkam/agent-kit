// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Retains exact terminal evidence alongside the portable disposition of one tool call.
/// </summary>
/// <remarks>
/// This immutable projection value preserves the source status, including unknown numeric values,
/// independently of its coarser model-facing kind. It neither proves terminal recording nor grants
/// permission to repeat an effect. A projector must additionally record status coarsening in
/// <see cref="ToolResultProjectionInfo"/> when it encounters an unknown source status.
/// </remarks>
public sealed record ToolCallOutcome
{
    /// <summary>Captures a consistent portable outcome without inferring effect certainty from success or failure.</summary>
    /// <param name="kind">The closed portable category required by <paramref name="sourceStatus"/>.</param>
    /// <param name="sourceStatus">The exact terminal status. Unknown numeric values are retained and require <see cref="ToolCallOutcomeKind.Failed"/>.</param>
    /// <param name="sideEffectCertainty">The evidence about the requested tool effect; <see cref="SideEffectCertainty.NotApplicable"/> is not valid for this boundary.</param>
    /// <param name="retryable">Retained advice about a future request under policy, never permission to replay a recorded effect.</param>
    /// <param name="failureReason">Optional safe model-facing failure information. Success requires null; other statuses do not require fabricated explanatory text.</param>
    /// <param name="extensions">Non-null immutable outcome extension data with initialized canonical value buffers.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> or <paramref name="sideEffectCertainty"/> is undefined, or certainty is <see cref="SideEffectCertainty.NotApplicable"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="kind"/> contradicts the source status, success has a failure reason, or an extension value has an uninitialized buffer.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ToolCallOutcome(
        ToolCallOutcomeKind kind,
        ToolTerminalStatus sourceStatus,
        SideEffectCertainty sideEffectCertainty,
        bool retryable,
        string? failureReason,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNotEqual(kind, sourceStatus.ToOutcomeKind(), nameof(kind));
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectCertainty);
        ArgumentOutOfRangeException.ThrowIfEqual(sideEffectCertainty, SideEffectCertainty.NotApplicable, nameof(sideEffectCertainty));
        if (kind is ToolCallOutcomeKind.Success)
        {
            ArgumentException.ThrowIfNotEqual(failureReason, null, nameof(failureReason));
        }
        ArgumentNullException.ThrowIfNull(extensions);
        foreach (var extension in extensions.Values)
        {
            ArgumentException.ThrowIfDefault(extension.Value.CanonicalJson, nameof(extensions));
        }

        Kind = kind;
        SourceStatus = sourceStatus;
        SideEffectCertainty = sideEffectCertainty;
        Retryable = retryable;
        FailureReason = failureReason;
        Extensions = extensions;
    }

    /// <summary>Gets the portable disposition consistent with the retained source status.</summary>
    /// <value>Success is possible only for <see cref="ToolTerminalStatus.Succeeded"/>; unknown statuses map to failure.</value>
    public ToolCallOutcomeKind Kind { get; }

    /// <summary>Gets the exact terminal status before portable coarsening.</summary>
    /// <value>The supplied status, including an unrecognized numeric value.</value>
    public ToolTerminalStatus SourceStatus { get; }

    /// <summary>Gets what is known about the requested effect independently of its disposition.</summary>
    /// <value>A defined certainty other than <see cref="SideEffectCertainty.NotApplicable"/>; failure and cancellation may retain unknown or partial effects.</value>
    public SideEffectCertainty SideEffectCertainty { get; }

    /// <summary>Gets retained retry advice for a future request evaluated under policy.</summary>
    /// <value>The supplied advice. This value alone never authorizes replay or establishes idempotency.</value>
    public bool Retryable { get; }

    /// <summary>
    /// Gets a safe, non-sensitive description of the failure, when
    /// <see cref="Kind"/> is not <see cref="ToolCallOutcomeKind.Success"/>.
    /// </summary>
    /// <value>Null for success; otherwise the supplied optional model-facing reason.</value>
    public string? FailureReason { get; }

    /// <summary>Gets provider- or tool-specific outcome data.</summary>
    /// <value>The non-null validated immutable extension bag retained at construction.</value>
    public ExtensionData Extensions { get; }
}

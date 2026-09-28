// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

using Microsoft.Extensions.Options;

/// <summary>Classifies recovery actions from captured evidence, idempotency, and the configured unknown-effect mode.</summary>
/// <remarks>
/// <para>
/// The policy is a pure decision function over recorded evidence. It performs no I/O, reads no ambient clock, and
/// never invokes an operation; the coordinator carries out whichever action it returns. Decisions follow evidence in
/// this precedence:
/// </para>
/// <list type="number">
/// <item><description>A recorded terminal result is committed and never reinvoked, whatever the effect certainty.</description></item>
/// <item><description>An operation waiting on an external owner resumes through that owner, honoring its recorded earliest retry instant.</description></item>
/// <item><description>A start proven absent may begin, because absence is evidence of non-start only when the protocol requires the record before every start.</description></item>
/// <item><description>Otherwise the effect certainty, the operation's idempotency classification, and <see cref="AgentDurabilityOptions.UnknownEffectMode"/> decide between retry, reconciliation, and operator action.</description></item>
/// </list>
/// <para>
/// An unknown outcome for a non-idempotent effect is never retried. Exactly-once is not achieved by adding optimism to
/// a retry loop, so such evidence yields reconciliation only when a backend reference exists and the profile permits
/// it, and operator action otherwise.
/// </para>
/// </remarks>
public sealed class DefaultRecoveryPolicy: IRecoveryPolicy
{
    private readonly IOptions<AgentDurabilityOptions> _options;

    /// <summary>Initializes the policy over the engine-wide durability options.</summary>
    /// <param name="options">
    /// The non-null options accessor supplying <see cref="AgentDurabilityOptions.UnknownEffectMode"/>. The accessor is
    /// read per decision so a reloaded configuration takes effect without recomposing the policy.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public DefaultRecoveryPolicy(IOptions<AgentDurabilityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> or <paramref name="evidence"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was already cancelled.</exception>
    public ValueTask<RecoveryDecision> DecideAsync(
        RecoverableOperationDescriptor operation,
        RecoveryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Decide(operation, evidence));
    }

    private RecoveryDecision Decide(RecoverableOperationDescriptor operation, RecoveryEvidence evidence)
    {
        Debug.Assert(operation is not null, "The public entry point validates the operation descriptor.");
        Debug.Assert(evidence is not null, "The public entry point validates the evidence.");

        return evidence switch
        {
            // A recorded terminal result outranks every certainty signal: the operation already reached its outcome.
            { TerminalResultRecorded: true, RecordedResult: { } recorded } => new RecoveryCommitRecordedResult(recorded),
            { State: DurableOperationState.Waiting } =>
                new RecoveryRetryOperation(evidence.NotBefore, evidence.ExternalIdempotencyKey),
            { StartDefinitelyAbsent: true } => new RecoveryStartOperation(),
            _ => DecideFromCertainty(operation, evidence),
        };
    }

    private RecoveryDecision DecideFromCertainty(
        RecoverableOperationDescriptor operation,
        RecoveryEvidence evidence) =>
        evidence.SideEffectCertainty switch
        {
            SideEffectCertainty.NotApplicable or SideEffectCertainty.DefinitelyNotPerformed =>
                evidence.State is DurableOperationState.Accepted
                    ? new RecoveryStartOperation()
                    : new RecoveryRetryOperation(evidence.NotBefore, evidence.ExternalIdempotencyKey),
            SideEffectCertainty.DefinitelyPerformed or SideEffectCertainty.PartiallyPerformed =>
                DecideAfterEffect(operation, evidence),
            SideEffectCertainty.Unknown => DecideUnknownEffect(operation, evidence),
            _ => new RecoveryNotPossible("The recorded side-effect certainty is not recognized by this build."),
        };

    private static RecoveryDecision DecideAfterEffect(
        RecoverableOperationDescriptor operation,
        RecoveryEvidence evidence) =>
        evidence.ExternalReference is { } reference
            ? new RecoveryReconcileOperation(reference)
            : operation.Idempotency switch
            {
                IdempotencyClassification.ReadOnly or IdempotencyClassification.Idempotent =>
                    new RecoveryRetryOperation(evidence.NotBefore, evidence.ExternalIdempotencyKey),
                IdempotencyClassification.IdempotentWithKey when evidence.ExternalIdempotencyKey is { } key =>
                    new RecoveryRetryOperation(evidence.NotBefore, key),
                IdempotencyClassification.IdempotentWithKey or IdempotencyClassification.NonIdempotent =>
                    new RecoveryRequiresOperator(
                        "The effect was performed but no terminal outcome, external reference, or usable idempotency key was recorded."),
                _ => new RecoveryNotPossible("The declared idempotency classification is not recognized by this build."),
            };

    private RecoveryDecision DecideUnknownEffect(RecoverableOperationDescriptor operation, RecoveryEvidence evidence) =>
        operation.Idempotency switch
        {
            IdempotencyClassification.ReadOnly or IdempotencyClassification.Idempotent =>
                new RecoveryRetryOperation(evidence.NotBefore, evidence.ExternalIdempotencyKey),
            IdempotencyClassification.IdempotentWithKey when evidence.ExternalIdempotencyKey is { } key =>
                new RecoveryRetryOperation(evidence.NotBefore, key),
            IdempotencyClassification.IdempotentWithKey when evidence.ExternalReference is { } reference =>
                new RecoveryReconcileOperation(reference),
            IdempotencyClassification.IdempotentWithKey =>
                new RecoveryRequiresOperator(
                    "The outcome is unknown and the receiver idempotency key required to retry safely was not recorded."),
            IdempotencyClassification.NonIdempotent
                when _options.Value.UnknownEffectMode is UnknownEffectRecoveryMode.ReconcileWhenSupported
                && evidence.ExternalReference is { } reconcilable =>
                new RecoveryReconcileOperation(reconcilable),
            IdempotencyClassification.NonIdempotent =>
                new RecoveryRequiresOperator(
                    "The external effect outcome is unknown for a non-idempotent operation."),
            _ => new RecoveryNotPossible("The operation's idempotency classification is not recognized by this build."),
        };
}

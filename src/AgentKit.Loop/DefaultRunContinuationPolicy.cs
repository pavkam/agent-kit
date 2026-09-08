// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Applies the canonical strict precedence to one immutable continuation snapshot.</summary>
/// <remarks>This stateless policy performs no I/O and returns proposals only; the session owner revalidates and commits transitions.</remarks>
internal sealed class DefaultRunContinuationPolicy: IRunContinuationPolicy
{
    /// <inheritdoc/>
    public ValueTask<RunContinuationDecision> DecideAsync(
        RunContinuationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.State is AgentRunState.Cancelling
            or AgentRunState.Failing
            or AgentRunState.Settling
            or AgentRunState.Settled)
        {
            return Invalid($"State '{context.State}' is not an ordinary continuation boundary.");
        }

        if (context.RequiredStopOutcome is { } stop)
        {
            return ValueTask.FromResult<RunContinuationDecision>(new HaltRun(stop));
        }

        if (context.Boundary is CommittedTurnContinuationBoundary
            {
                OutputDecision: OutputRejected rejected,
            })
        {
            return ValueTask.FromResult<RunContinuationDecision>(new HaltRun(new AgentRunOutputRejected(rejected)));
        }

        if (context.Boundary is CommittedTurnContinuationBoundary
            {
                OutputDecision: OutputConfigurationRejected configurationRejected,
            })
        {
            return ValueTask.FromResult<RunContinuationDecision>(
                new HaltRun(new AgentRunOutputRejected(configurationRejected)));
        }

        if (context.Boundary is CommittedTurnContinuationBoundary
            {
                RequiresOutputValidation: true,
                OutputDecision: not OutputAccepted and not OutputRetryRequired,
            })
        {
            return Invalid("Required output validation is missing a terminal processor decision.");
        }

        if (context.Boundary is CommittedTurnContinuationBoundary { OutputDecision: OutputRetryRequired retry }
            && !context.Causes.OfType<OutputRepairContinuationCause>().Any(cause => cause.Decision.Equals(retry)))
        {
            return Invalid("An output retry decision requires matching repair continuation evidence.");
        }

        if (context.Boundary is CommittedTurnContinuationBoundary { ToolResults.IsEmpty: false } toolBoundary
            && !context.Causes.OfType<CommittedToolResultsContinuationCause>()
                .Any(cause => cause.ToolResults.SequenceEqual(toolBoundary.ToolResults)))
        {
            return Invalid("Committed tool results require matching continuation evidence.");
        }

        if (!context.Causes.IsEmpty)
        {
            var selected = context.Causes
                .Select(static (cause, index) => (Cause: cause, Index: index, Priority: GetPriority(cause)))
                .OrderBy(static item => item.Priority)
                .ThenBy(static item => item.Index)
                .First();
            var pending = context.Causes.RemoveAt(selected.Index);
            return ValueTask.FromResult<RunContinuationDecision>(
                new ContinueRun(new ContinuationReason(selected.Cause, pending)));
        }

        return context.Boundary switch
        {
            CommittedTurnContinuationBoundary { ToolResults.IsEmpty: false } =>
                Invalid("Committed tool results require explicit continuation evidence."),
            CommittedTurnContinuationBoundary { RequiresOutputValidation: true, OutputDecision: not OutputAccepted } =>
                Invalid("Required output validation is missing an accepted terminal decision."),
            CommittedTurnContinuationBoundary committed =>
                ValueTask.FromResult<RunContinuationDecision>(new CompleteRun(new AgentRunCompleted(committed.Response))),
            IdleContinuationBoundary =>
                ValueTask.FromResult<RunContinuationDecision>(new CompleteRun(new AgentRunIdle())),
            RetryContinuationBoundary =>
                Invalid("A retry boundary requires explicit retry evidence."),
            DeferredContinuationBoundary =>
                Invalid("A deferred boundary requires matching completion evidence."),
            _ => Invalid("The continuation boundary is not recognized."),
        };
    }

    private static int GetPriority(RunContinuationCause cause) => cause switch
    {
        PromotedInputContinuationCause => 0,
        CommittedToolResultsContinuationCause => 1,
        OutputRepairContinuationCause => 2,
        DeferredCompletionContinuationCause => 3,
        CompactionRetryContinuationCause => 4,
        ExplicitPolicyContinuationCause => 5,
        _ => int.MaxValue,
    };

    private static ValueTask<RunContinuationDecision> Invalid(string safeMessage) =>
        ValueTask.FromResult<RunContinuationDecision>(new HaltRun(new AgentRunInvalidState(safeMessage)));
}

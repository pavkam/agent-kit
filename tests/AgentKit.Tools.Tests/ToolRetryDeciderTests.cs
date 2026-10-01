// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using static ToolRuntimeTestCalls;

/// <summary>Verifies <see cref="ToolRetryDecider"/> eligibility for every effect, idempotency, certainty, and enforcement combination.</summary>
public sealed class ToolRetryDeciderTests
{
    private static readonly ToolRetryPolicy _policy = new(3, TimeSpan.Zero, 1.0, TimeSpan.Zero, 0.0);
    private static readonly ToolEffects _readOnly = new(ToolEffect.ReadOnly, null, null);

    [Fact]
    public void Decline_WhenAttemptSucceeded_ReportsSucceeded() =>
        ToolRetryDecider.Decline(Result(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, retryable: true), Context(_readOnly), new PlainInvoker(), _policy, 1)
            .ShouldBe("succeeded");

    [Fact]
    public void Decline_WhenAttemptWasCancelled_ReportsCancelled() =>
        ToolRetryDecider.Decline(Result(ToolCallOutcomeKind.Cancelled, ToolTerminalStatus.Interrupted, SideEffectCertainty.Unknown, retryable: true), Context(_readOnly), new PlainInvoker(), _policy, 1)
            .ShouldBe("cancelled");

    [Fact]
    public void Decline_WhenFailureIsNotRetryable_ReportsNotRetryable() =>
        ToolRetryDecider.Decline(Failed(SideEffectCertainty.DefinitelyNotPerformed, retryable: false), Context(_readOnly), new PlainInvoker(), _policy, 1)
            .ShouldBe("not_retryable");

    [Fact]
    public void Decline_WhenAttemptBudgetIsSpent_ReportsExhausted() =>
        ToolRetryDecider.Decline(Failed(SideEffectCertainty.DefinitelyNotPerformed, retryable: true), Context(_readOnly), new PlainInvoker(), _policy, 3)
            .ShouldBe("exhausted");

    [Theory]
    [InlineData(SideEffectCertainty.DefinitelyNotPerformed)]
    [InlineData(SideEffectCertainty.Unknown)]
    [InlineData(SideEffectCertainty.PartiallyPerformed)]
    [InlineData(SideEffectCertainty.DefinitelyPerformed)]
    public void Decline_WhenReadOnlyCallFailsRetryably_IsEligibleForAnyCertainty(SideEffectCertainty certainty) =>
        ToolRetryDecider.Decline(Failed(certainty, retryable: true), Context(_readOnly), new PlainInvoker(), _policy, 1).ShouldBeNull();

    [Fact]
    public void Decline_WhenMutatingCallWasDefinitelyNotPerformed_IsEligibleWithoutIdempotency() =>
        ToolRetryDecider.Decline(
            Failed(SideEffectCertainty.DefinitelyNotPerformed, retryable: true),
            Context(new ToolEffects(ToolEffect.Mutating, null, null)),
            new PlainInvoker(),
            _policy,
            1).ShouldBeNull();

    [Theory]
    [InlineData(SideEffectCertainty.Unknown)]
    [InlineData(SideEffectCertainty.PartiallyPerformed)]
    [InlineData(SideEffectCertainty.DefinitelyPerformed)]
    public void Decline_WhenMutatingCallMayHaveStartedAndInvokerIsPlain_IsUnsafeEvenIfDeclaredIdempotent(SideEffectCertainty certainty) =>
        ToolRetryDecider.Decline(
            Failed(certainty, retryable: true),
            Context(new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.Idempotent, null)),
            new PlainInvoker(),
            _policy,
            1).ShouldBe("unsafe");

    [Fact]
    public void Decline_WhenIdempotentMutationIsEnforcedByTheInvoker_IsEligible() =>
        ToolRetryDecider.Decline(
            Failed(SideEffectCertainty.Unknown, retryable: true),
            Context(new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.Idempotent, null)),
            new EnforcingInvoker(true),
            _policy,
            1).ShouldBeNull();

    [Fact]
    public void Decline_WhenInvokerRefusesToEnforce_IsUnsafe() =>
        ToolRetryDecider.Decline(
            Failed(SideEffectCertainty.Unknown, retryable: true),
            Context(new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.Idempotent, null)),
            new EnforcingInvoker(false),
            _policy,
            1).ShouldBe("unsafe");

    [Fact]
    public void Decline_WhenKeyedIdempotencyHasItsKeyAndIsEnforced_IsEligible() =>
        ToolRetryDecider.Decline(
            Failed(SideEffectCertainty.Unknown, retryable: true),
            Context(new ToolEffects(ToolEffect.Mutating, IdempotencyClassification.IdempotentWithKey, null), new IdempotencyKey("key")),
            new EnforcingInvoker(true),
            _policy,
            1).ShouldBeNull();

    [Fact]
    public void Decline_WhenMutationDeclaresNoIdempotency_IsUnsafeEvenIfInvokerEnforces() =>
        ToolRetryDecider.Decline(
            Failed(SideEffectCertainty.Unknown, retryable: true),
            Context(new ToolEffects(ToolEffect.Mutating, null, null)),
            new EnforcingInvoker(true),
            _policy,
            1).ShouldBe("unsafe");

    [Fact]
    public void Decline_WhenArgumentsAreInvalid_ThrowsExactParameter()
    {
        var failed = Failed(SideEffectCertainty.Unknown, retryable: true);
        var context = Context(_readOnly);
        var invoker = new PlainInvoker();

        Should.Throw<ArgumentNullException>(() => ToolRetryDecider.Decline(null!, context, invoker, _policy, 1)).ParamName.ShouldBe("invocation");
        Should.Throw<ArgumentNullException>(() => ToolRetryDecider.Decline(failed, null!, invoker, _policy, 1)).ParamName.ShouldBe("context");
        Should.Throw<ArgumentNullException>(() => ToolRetryDecider.Decline(failed, context, null!, _policy, 1)).ParamName.ShouldBe("invoker");
        Should.Throw<ArgumentNullException>(() => ToolRetryDecider.Decline(failed, context, invoker, null!, 1)).ParamName.ShouldBe("policy");
        Should.Throw<ArgumentOutOfRangeException>(() => ToolRetryDecider.Decline(failed, context, invoker, _policy, 0)).ParamName.ShouldBe("attempt");
    }

    private static ToolInvocationResult Failed(SideEffectCertainty certainty, bool retryable) =>
        Result(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, certainty, retryable);

    private static ToolInvocationResult Result(ToolCallOutcomeKind kind, ToolTerminalStatus status, SideEffectCertainty certainty, bool retryable) =>
        new(new ToolCallOutcome(kind, status, certainty, retryable, kind is ToolCallOutcomeKind.Success ? null : "reason", ExtensionData.Empty), []);

    private sealed class PlainInvoker: IToolInvoker
    {
        public ValueTask<ToolInvocationResult> InvokeAsync(ToolInvocationContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class EnforcingInvoker(bool enforces): IIdempotencyEnforcingToolInvoker
    {
        public bool EnforcesIdempotency(ToolInvocationContext context) => enforces;

        public ValueTask<ToolInvocationResult> InvokeAsync(ToolInvocationContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

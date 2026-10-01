// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using static ToolRuntimeTestCalls;

/// <summary>Verifies <see cref="ToolCallResultComposer"/> builds each terminal record from retained admission, acceptance, and policy evidence.</summary>
public sealed class ToolCallResultComposerTests
{
    [Fact]
    public void FromAcceptedInvocation_WhenInvocationSucceeded_CarriesAcceptanceGrantAndExactPolicyEvidence()
    {
        var entry = Entry(new ToolEffects(ToolEffect.ReadOnly, null, null));
        var invocation = Result(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, retryable: false);
        var started = DateTimeOffset.UnixEpoch.AddSeconds(1);

        var result = ToolCallResultComposer.FromAcceptedInvocation(
            entry, invocation, new ToolResultNormalized([], new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty)),
            started, started.AddSeconds(1));

        result.Status.ShouldBe(ToolTerminalStatus.Succeeded);
        result.Acceptance.ShouldBe(entry.Accepted.Acceptance);
        result.GrantId.ShouldBe(entry.Accepted.Acceptance.InvocationGrantId);
        result.Admission.ShouldBe(entry.Accepted.Admission);
        result.Normalization.ShouldBe(entry.Accepted.Normalization);
        result.ProjectionPolicy.ShouldBe(entry.Accepted.ProjectionPolicy);
        result.InvocationStartedAt.ShouldBe(started);
        result.ProviderAlias.ShouldBe(entry.Accepted.ProviderAlias);
    }

    [Fact]
    public void FromAcceptedInvocation_WhenMutatingCallMayHaveStartedWithoutIdempotency_NeverAdvertisesARetry()
    {
        var entry = Entry(new ToolEffects(ToolEffect.Mutating, null, null));
        var failed = Result(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown, retryable: true);

        var result = ToolCallResultComposer.FromAcceptedInvocation(
            entry, failed, new ToolResultNormalized([], new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty)),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

        result.Retryable.ShouldBeFalse();
        result.Error!.SafeMessage.ShouldBe("reason");
    }

    [Fact]
    public void FromAcceptedInvocation_WhenNormalizationFailed_ReportsTheFailureWithTheInvocationCertainty()
    {
        var entry = Entry(new ToolEffects(ToolEffect.ReadOnly, null, null));
        var invocation = Result(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, retryable: false);

        var result = ToolCallResultComposer.FromAcceptedInvocation(
            entry, invocation, new ToolResultNormalizationFailed(ToolTerminalStatus.ResultNormalizationFailed, "Too large."),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

        result.Status.ShouldBe(ToolTerminalStatus.ResultNormalizationFailed);
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyPerformed);
        result.Error!.Kind.ShouldBe(ToolErrorKind.Serialization);
    }

    [Fact]
    public void AcceptedNotStarted_WhenCalled_HasAcceptanceNoStartAndDefinitelyNotPerformed()
    {
        var entry = Entry(new ToolEffects(ToolEffect.ReadOnly, null, null));

        var result = ToolCallResultComposer.AcceptedNotStarted(entry, ToolTerminalStatus.Interrupted, "Interrupted.", DateTimeOffset.UnixEpoch);

        result.Acceptance.ShouldBe(entry.Accepted.Acceptance);
        result.InvocationStartedAt.ShouldBeNull();
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Retryable.ShouldBeFalse();
    }

    [Fact]
    public void AcceptedInterrupted_WhenCalled_HasUnknownCertaintyAndTheAttemptStart()
    {
        var entry = Entry(new ToolEffects(ToolEffect.Mutating, null, null));
        var started = DateTimeOffset.UnixEpoch.AddSeconds(2);

        var result = ToolCallResultComposer.AcceptedInterrupted(entry, started, started.AddSeconds(1));

        result.Status.ShouldBe(ToolTerminalStatus.Interrupted);
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        result.InvocationStartedAt.ShouldBe(started);
    }

    [Fact]
    public void WithoutContent_WhenResultCarriesContent_ReturnsAnEqualRecordWithNoContent()
    {
        var entry = Entry(new ToolEffects(ToolEffect.ReadOnly, null, null));
        var success = Result(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, retryable: false);
        var result = ToolCallResultComposer.FromAcceptedInvocation(
            entry, success,
            new ToolResultNormalized(
                [new ToolResultTextContent("body", TextSemantics.Plain, ExtensionData.Empty)],
                new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty)),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

        var stripped = ToolCallResultComposer.WithoutContent(result);

        stripped.Content.ShouldBeEmpty();
        stripped.CallId.ShouldBe(result.CallId);
        stripped.Status.ShouldBe(result.Status);
        stripped.Acceptance.ShouldBe(result.Acceptance);
        ToolCallResultComposer.WithoutContent(stripped).ShouldBeSameAs(stripped);
    }

    [Fact]
    public void PreInvocation_WhenAGrantWasIssued_RetainsItWithoutAcceptance()
    {
        var request = Request();
        var grant = new GrantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        var result = ToolCallResultComposer.PreInvocation(
            request, ToolTerminalStatus.Unsupported, "Not recorded.", new ToolId("tool.write"), new ToolVersion("1"),
            new ToolEffects(ToolEffect.ReadOnly, null, null), ToolRuntimeNormalizationDefaults.ForResolvedTool(Standard),
            DateTimeOffset.UnixEpoch, grant);

        result.GrantId.ShouldBe(grant);
        result.Acceptance.ShouldBeNull();
        result.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
    }

    private static ToolCallRequest Request() => new(
        AgentId, SessionId, RunId, TurnId, OperationId, new ToolCallId(Guid.Parse("66666666-6666-6666-6666-666666666661")),
        Authorization(), new ToolCatalogVersion("catalog-1"), 0, new ToolAlias("write"), [123, 125], DateTimeOffset.UnixEpoch);

    private static ToolBatchEntry Entry(ToolEffects effects)
    {
        var context = Context(effects);
        var tool = context.Tool;
        var normalization = ToolRuntimeNormalizationDefaults.ForResolvedTool(Standard);
        var fingerprint = new InputFingerprint("sha256:validated");
        var validated = new ValidatedToolCall(
            AgentId, SessionId, RunId, TurnId, OperationId, context.CallId, context.InvocationGrant.Authorization,
            new ToolCatalogVersion("catalog-1"), new ToolAlias("write"), tool, tool.Version, Standard, 0, context.Arguments,
            fingerprint, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var plan = new ToolExecutionPlan(tool.ExecutionHints, ToolRetryPolicy.NoRetry, TimeSpan.FromMinutes(1), normalization);
        var accepted = new AcceptedToolCall(
            AgentId, SessionId, RunId, TurnId, OperationId, context.CallId, context.InvocationGrant.Authorization,
            new ToolCallAcceptanceEvidence(context.InvocationGrant.Id, fingerprint, DateTimeOffset.UnixEpoch),
            new ToolAlias("write"), tool.Id, tool.Version, effects, externalIdempotencyKey: null,
            new ToolCallAdmissionEvidence(new ToolCatalogVersion("catalog-1"), 0, new InputFingerprint("sha256:raw")),
            normalization, normalization.ProjectionPolicy, DateTimeOffset.UnixEpoch);
        return new ToolBatchEntry(context, new NullLease(tool), new PreparedToolCall(validated, plan), accepted);
    }

    private static ToolInvocationResult Result(ToolCallOutcomeKind kind, ToolTerminalStatus status, SideEffectCertainty certainty, bool retryable) =>
        new(new ToolCallOutcome(kind, status, certainty, retryable, kind is ToolCallOutcomeKind.Success ? null : "reason", ExtensionData.Empty), []);

    private sealed class NullLease(ToolDescriptor tool): IToolInvokerLease
    {
        public ToolDescriptor Tool { get; } = tool;
        public ToolSourceVersion SourceVersion { get; } = new("1");
        public IToolInvoker Invoker => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan.Tests;



/// <summary>Verifies PlanTool behavior and contracts.</summary>
public sealed class PlanToolTests
{
    private const string ReplaceArguments = /*lang=json,strict*/ """
        {
          "action": "replace",
          "title": "Ship it",
          "items": [
            { "id": "one", "text": "First step.", "status": "in_progress" },
            { "id": "two", "text": "Second step.", "status": "pending" }
          ]
        }
        """;
    [Theory]
    [InlineData( /*lang=json,strict*/"{}")]
    [InlineData( /*lang=json,strict*/"{\"action\":\"get\",\"title\":\"extra\"}")]
    [InlineData( /*lang=json,strict*/"{\"action\":\"replace\",\"title\":\"T\",\"items\":[]}")]
    [InlineData( /*lang=json,strict*/"{\"action\":\"replace\",\"title\":\"T\",\"items\":[{\"id\":\"x\",\"text\":\"X\",\"status\":\"in_progress\"},{\"id\":\"y\",\"text\":\"Y\",\"status\":\"in_progress\"}]}")]
    [InlineData( /*lang=json,strict*/"{\"action\":\"set_status\",\"item_id\":\"x\",\"status\":\"completed\"}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_PerformsNoAuthorizationOrStateAccess(string json)
    {
        var store = new RecordingPlanStateStore();
        var authority = new RecordingSecurityAuthority();
        var ids = new FixedSecurityRequestIdGenerator();
        var result = await Tool(store, authority, ids).InvokeAsync(Request(json), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        ids.Calls.ShouldBe(0);
        authority.Requests.ShouldBeEmpty();
        store.Reads.ShouldBeEmpty();
        store.Replacements.ShouldBeEmpty();
        store.StatusChanges.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSessionMissing_PerformsNoAuthorizationOrStateAccess()
    {
        var store = new RecordingPlanStateStore();
        var authority = new RecordingSecurityAuthority();
        var result = await Tool(store, authority).InvokeAsync(Request( /*lang=json,strict*/"{\"action\":\"get\"}", includeSession: false), TestContext.Current.CancellationToken);
        result.Outcome.FailureReason!.ShouldContain("requires a session");
        authority.Requests.ShouldBeEmpty();
        store.Reads.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenReading_BindsExactObserveGrantAndProjectsMissingAsSuccess()
    {
        var store = new RecordingPlanStateStore();
        var authority = new RecordingSecurityAuthority();
        var result = await Tool(store, authority).InvokeAsync(Request( /*lang=json,strict*/"{\"action\":\"get\"}"), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Kind.ShouldBe(SecurityOperationKind.StateRead);
        security.Effect.ShouldBe(SecurityEffect.Observe);
        security.Audience.ShouldBe(store.SecurityAudience);
        security.InputFingerprint.ShouldBe(PlanSecurityBinding.ReadFingerprint(TestData.Context.ToAddress()));
        store.Reads.ShouldHaveSingleItem().Grant.RequestId.ShouldBe(security.Id);
        Json(result).RootElement.GetProperty("plan").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task InvokeAsync_WhenReplacing_BindsFullMutationAndProjectsVersionedPlan()
    {
        var plan = TestData.Plan();
        var store = new RecordingPlanStateStore
        {
            Result = new PlanStateFound(plan)
        };
        var authority = new RecordingSecurityAuthority();
        var result = await Tool(store, authority).InvokeAsync(Request(ReplaceArguments), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var replacement = store.Replacements.ShouldHaveSingleItem();
        replacement.ExpectedRevision.ShouldBeNull();
        replacement.Items.Count(static item => item.Status == PlanItemStatus.InProgress).ShouldBe(1);
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Kind.ShouldBe(SecurityOperationKind.StateMutation);
        security.Effect.ShouldBe(SecurityEffect.Mutate);
        security.InputFingerprint.ShouldBe(PlanSecurityBinding.ReplaceFingerprint(TestData.Context.ToAddress(), replacement.Title, replacement.Items, null));
        using var json = Json(result);
        json.RootElement.GetProperty("plan").GetProperty("revision").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task InvokeAsync_WhenSettingStatus_RequiresAndBindsExpectedRevision()
    {
        var store = new RecordingPlanStateStore
        {
            Result = new PlanStateFound(TestData.Plan(3))
        };
        var authority = new RecordingSecurityAuthority();
        _ = await Tool(store, authority).InvokeAsync(Request( /*lang=json,strict*/"{\"action\":\"set_status\",\"item_id\":\"one\",\"status\":\"completed\",\"expected_revision\":2}"), TestContext.Current.CancellationToken);
        var change = store.StatusChanges.ShouldHaveSingleItem();
        change.ExpectedRevision.ShouldBe(new PlanRevision(2));
        change.Status.ShouldBe(PlanItemStatus.Completed);
        authority.Requests.ShouldHaveSingleItem().InputFingerprint.ShouldBe(PlanSecurityBinding.StatusFingerprint(TestData.Context.ToAddress(), new PlanItemId("one"), PlanItemStatus.Completed, new PlanRevision(2)));
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorityDenies_ReturnsRejectedWithoutStateAccess()
    {
        var store = new RecordingPlanStateStore();
        var result = await Tool(store, new RecordingSecurityAuthority(false)).InvokeAsync(Request( /*lang=json,strict*/"{\"action\":\"get\"}"), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        store.Reads.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenStoreRejectsGrant_ReturnsRejected()
    {
        var store = new RecordingPlanStateStore
        {
            Result = new PlanStateDenied("Grant mismatch.")
        };
        var result = await Tool(store, new RecordingSecurityAuthority()).InvokeAsync(Request( /*lang=json,strict*/"{\"action\":\"get\"}"), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.FailureReason.ShouldBe("Grant mismatch.");
    }

    [Fact]
    public async Task InvokeAsync_WhenRevisionConflicts_ReturnsCurrentRevisionWithoutContent()
    {
        var store = new RecordingPlanStateStore
        {
            Result = new PlanStateConflict(new PlanRevision(7))
        };
        var result = await Tool(store, new RecordingSecurityAuthority()).InvokeAsync(Request(ReplaceArguments), TestContext.Current.CancellationToken);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason!.ShouldContain("revision is 7");
        result.Content.ShouldBeEmpty();
    }

    private static PlanTool Tool(IPlanStateStore store, ISecurityAuthority authority, FixedSecurityRequestIdGenerator? ids = null) => new(store, authority, ids ?? new FixedSecurityRequestIdGenerator(), new FixedTimeProvider(), Options.Create(new PlanToolOptions()));
    private static ToolInvocationRequest Request(string json, bool includeSession = true) => new(TestSupport.TestSecurityEvidence.ToolContext(TestData.AgentId, includeSession ? TestData.SessionId : null, TestData.ToolCallId, TestData.Correlation, TestData.Identity), JsonDocument.Parse(json).RootElement, DateTimeOffset.UnixEpoch);
    private static JsonDocument Json(ToolInvocationResult result) => JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);

}

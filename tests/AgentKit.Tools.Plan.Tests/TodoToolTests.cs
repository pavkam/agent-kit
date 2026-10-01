// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan.Tests;

using AgentKit.TestSupport;



/// <summary>Verifies TodoTool behavior and contracts.</summary>
public sealed class TodoToolTests
{
    [Fact]
    public async Task TodoTool_WhenInvoked_UsesSameCanonicalStateAndSecurityBinding()
    {
        var store = new RecordingPlanStateStore();
        var authority = new RecordingSecurityAuthority();
        var tool = new TodoTool(store, new FixedSecurityAuthoritySelector(authority), new FixedSecurityRequestIdGenerator(), new FixedTimeProvider(), Options.Create(new PlanToolOptions()), NullLogger<PlanTool>.Instance);
        var result = await tool.InvokeAsync(Request( /*lang=json,strict*/"{\"action\":\"get\"}"), TestContext.Current.CancellationToken);
        TodoTool.Descriptor.Id.ShouldBe(TodoTool.Id);
        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        _ = store.Reads.ShouldHaveSingleItem();
        authority.Requests.ShouldHaveSingleItem().InputFingerprint.ShouldBe(PlanSecurityBinding.ReadFingerprint(TestData.Context.ToAddress()));
    }

    private static ToolInvocationContext Request(string json, bool includeSession = true) => ToolCaptureTestData.FromRequest(new(TestSecurityEvidence.ToolContext(TestData.AgentId, includeSession ? TestData.SessionId : null, TestData.ToolCallId, TestData.Correlation, TestData.Identity), JsonDocument.Parse(json).RootElement, DateTimeOffset.UnixEpoch), TodoTool.Descriptor);
}

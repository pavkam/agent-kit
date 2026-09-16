// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionFailed behavior and contracts.</summary>
public sealed class CompactionFailedTests
{
    [Fact]
    public void CompactionFailed_Equality_WhenSameValues_InstancesAreEqual() => new CompactionFailed(Context(), Failure()).ShouldBe(new CompactionFailed(Context(), Failure()));
    private static readonly Guid _fixedOperationGuid = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid _fixedRunGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static InRunOperationCorrelation Correlation() => new(new OperationId(_fixedOperationGuid), new RunId(_fixedRunGuid), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
    // Fixed GUIDs make the "same values" equality assertions above
    // deterministic without threading a shared instance through every
    // helper.
    private static readonly Guid _fixedCompactionGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _fixedAgentGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _fixedSessionGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static CompactionOperationContext Context() => TestSupport.TestSecurityEvidence.CompactionContext(new CompactionId(_fixedCompactionGuid), new AgentId(_fixedAgentGuid), new SessionId(_fixedSessionGuid), Correlation(), Identity());
    private static CompactionFailure Failure() => new(CompactionFailureKind.Unknown, "unknown", retryable: false, ExtensionData.Empty);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new CompactionFailed(Context(), Failure());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionManifest behavior and contracts.</summary>
public sealed class CompactionManifestTests
{
    [Fact]
    public void CompactionManifest_Equality_WhenSameValues_InstancesAreEqual() => Manifest().ShouldBe(Manifest());
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
    private static CompactionSourceRange Range() => new(new SessionSequence(1), new SessionSequence(2));
    private static CompactionProducer Producer() => new(new CompactionStrategyKey("test"), deterministic: true, ExtensionData.Empty);
    private static CompactionManifest Manifest() => new(new CompactionManifestId(Guid.Parse("66666666-6666-6666-6666-666666666666")), Context(), new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")), new SessionVersion(1), Range(), new SessionSequence(3), Producer(), new ContextEpoch(0), new CompactionSizeEstimate(10, 10, 1), new CompactionSizeEstimate(5, 5, 1), DateTimeOffset.UnixEpoch, ExtensionData.Empty);
}

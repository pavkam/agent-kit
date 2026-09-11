// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;
/// <summary>Verifies ProcessRunResult behavior and contracts.</summary>
public sealed class ProcessRunResultTests
{
    [Fact]
    public void ProcessRunResult_WhenArtifactReferencesMatch_IsStructurallyEqual()
    {
        var reference = Reference();
        var left = Result(reference);
        var right = Result(reference);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    private static ProcessRunResult Result(ArtifactReference reference) => new(ProcessRunStatus.Exited, 0, [], [], 10, 0, true, false, ProcessSideEffectCertainty.Completed, null, reference);
    private static ArtifactReference Reference(ArtifactId? id = null, string version = "1") => new(id ?? new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion(version), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    [Fact]
    public void ProcessRunResult_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = new ProcessRunResult(ProcessRunStatus.Exited, 0, [1, 2], [3], 2, 1, false, false, ProcessSideEffectCertainty.Completed, null);
        var right = new ProcessRunResult(ProcessRunStatus.Exited, 0, [1, 2], [3], 2, 1, false, false, ProcessSideEffectCertainty.Completed, null);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void ProcessRunResult_WhenNonExitedHasExitCode_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ProcessRunResult(ProcessRunStatus.Denied, 1, [], [], 0, 0, false, false, ProcessSideEffectCertainty.NotStarted, "Denied."));
        exception.ParamName.ShouldBe("exitCode");
    }
}

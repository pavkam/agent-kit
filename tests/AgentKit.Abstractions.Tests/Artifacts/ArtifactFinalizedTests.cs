// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactFinalized behavior and contracts.</summary>
public sealed class ArtifactFinalizedTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var reference = Reference();
        var finalized = new ArtifactFinalized(reference);
        finalized.Reference.ShouldBe(reference);
        finalized.ToString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ArtifactFinalized(null!));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ArtifactFinalized(Reference());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ArtifactReference Reference(ArtifactId? id = null, string version = "1") => new(id ?? new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion(version), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"), Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}

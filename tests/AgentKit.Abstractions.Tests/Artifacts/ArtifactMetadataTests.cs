// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;



/// <summary>Verifies ArtifactMetadata behavior and contracts.</summary>
public sealed class ArtifactMetadataTests
{
    [Fact]
    public void ArtifactMetadata_WhenLengthIsNegative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Metadata(declaredLength: -1));
        exception.ParamName.ShouldBe("declaredLength");
    }

    private static ArtifactMetadata Metadata(long declaredLength = 7) => new(new ArtifactOwnerId("session:owner"), "text/plain", declaredLength, new ContentHash("hash"), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false));
}

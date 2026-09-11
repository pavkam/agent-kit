// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolResultArtifactContent behavior and contracts.</summary>
public sealed class ToolResultArtifactContentTests
{
    [Fact]
    public void ToolResultArtifactContent_Constructor_WhenReferenceNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultArtifactContent(null!, ExtensionData.Empty));
        exception.ParamName.ShouldBe("reference");
    }

    [Fact]
    public void ToolResultArtifactContent_Constructor_WhenExtensionsNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultArtifactContent(Artifact(), null!));
        exception.ParamName.ShouldBe("extensions");
    }

    private static ArtifactReference Artifact() => new(new ArtifactId(Guid.NewGuid()), new ArtifactVersion("1"), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), new TenantId("tenant"), new ArtifactOwnerId("session:owner"), new PrincipalId("principal"), "text/plain", 1, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch);
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

/// <summary>Verifies <see cref="ArtifactCoordinatorSnapshot"/> validation and content equality.</summary>
public sealed class ArtifactCoordinatorSnapshotTests
{
    private static readonly ComponentKey<IArtifactCoordinator> _key = new("coordinator");
    private static readonly ArtifactProfileKey _profile = new("profile");

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var snapshot = Create();

        snapshot.Key.ShouldBe(_key);
        snapshot.ProfileKey.ShouldBe(_profile);
        snapshot.ProfileVersion.ShouldBe(new ArtifactProfileVersion(2));
        snapshot.Backends.ShouldBe([new ArtifactBackendKey("a"), new ArtifactBackendKey("b")]);
    }

    [Fact]
    public void Constructor_WhenAValueIsInvalid_ThrowsNamingIt()
    {
        Should.Throw<ArgumentException>(() => new ArtifactCoordinatorSnapshot(default, _profile, new ArtifactProfileVersion(1), [new ArtifactBackendKey("a")])).ParamName.ShouldBe("key");
        Should.Throw<ArgumentException>(() => new ArtifactCoordinatorSnapshot(_key, default, new ArtifactProfileVersion(1), [new ArtifactBackendKey("a")])).ParamName.ShouldBe("profileKey");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactCoordinatorSnapshot(_key, _profile, default, [new ArtifactBackendKey("a")])).ParamName.ShouldBe("profileVersion");
        Should.Throw<ArgumentException>(() => new ArtifactCoordinatorSnapshot(_key, _profile, new ArtifactProfileVersion(1), default)).ParamName.ShouldBe("backends");
        Should.Throw<ArgumentException>(() => new ArtifactCoordinatorSnapshot(_key, _profile, new ArtifactProfileVersion(1), [])).ParamName.ShouldBe("backends");
        Should.Throw<ArgumentException>(() => new ArtifactCoordinatorSnapshot(_key, _profile, new ArtifactProfileVersion(1), [default])).ParamName.ShouldBe("backends");
        Should.Throw<ArgumentException>(() => new ArtifactCoordinatorSnapshot(_key, _profile, new ArtifactProfileVersion(1), [new ArtifactBackendKey("a"), new ArtifactBackendKey("a")])).ParamName.ShouldBe("backends");
    }

    [Fact]
    public void Equals_WhenContentMatches_ComparesBackendsByContent()
    {
        var first = Create();
        var second = Create();

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.Equals(new ArtifactCoordinatorSnapshot(_key, _profile, new ArtifactProfileVersion(2), [new ArtifactBackendKey("a")])).ShouldBeFalse();
        first.Equals(null).ShouldBeFalse();
    }

    private static ArtifactCoordinatorSnapshot Create() =>
        new(_key, _profile, new ArtifactProfileVersion(2), [new ArtifactBackendKey("a"), new ArtifactBackendKey("b")]);
}

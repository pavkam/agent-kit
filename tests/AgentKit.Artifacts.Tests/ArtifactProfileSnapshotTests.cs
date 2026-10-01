// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Verifies <see cref="ArtifactProfileSnapshot"/> validation, capture, and content equality.</summary>
public sealed class ArtifactProfileSnapshotTests
{
    private static readonly ArtifactProfileKey _key = new("profile");
    private static readonly ArtifactDirectoryId _directory = new("out");
    private static readonly ArtifactBackendKey _backend = new("backend");

    [Fact]
    public void Create_WhenOptionsAreValid_CapturesAnImmutableCopy()
    {
        var options = Valid();
        options.DefaultRetention = new ArtifactRetention(new ArtifactRetentionPolicyKey("keep"), null, true);
        _ = options.AllowedMutability.Add(ArtifactMutability.AppendOnly);
        options.AllowExternalOwnership = true;

        var snapshot = ArtifactProfileSnapshot.Create(_key, options);
        options.Routes[new ArtifactDirectoryId("later")] = new ArtifactBackendKey("later");
        _ = options.AllowedMutability.Remove(ArtifactMutability.Immutable);

        snapshot.Key.ShouldBe(_key);
        snapshot.Version.ShouldBe(new ArtifactProfileVersion(1));
        snapshot.DefaultDirectory.ShouldBe(_directory);
        snapshot.Routes.ShouldBe(new Dictionary<ArtifactDirectoryId, ArtifactBackendKey> { [_directory] = _backend });
        snapshot.DefaultRetention.ShouldBe(new ArtifactRetention(new ArtifactRetentionPolicyKey("keep"), null, true));
        snapshot.AllowedMutability.ShouldBe([ArtifactMutability.Immutable, ArtifactMutability.AppendOnly], ignoreOrder: true);
        snapshot.AllowExternalOwnership.ShouldBeTrue();
        snapshot.Backends().ShouldBe([_backend]);
    }

    [Fact]
    public void Create_WhenNoRetentionIsConfigured_DefaultsToADefaultPolicyWithoutExpiryOrHold()
    {
        var snapshot = ArtifactProfileSnapshot.Create(_key, Valid());

        snapshot.DefaultRetention.ShouldBe(new ArtifactRetention(new ArtifactRetentionPolicyKey("default"), null, false));
    }

    [Fact]
    public void Create_WhenArgumentsAreNullOrBlank_ThrowsNamingThem()
    {
        Should.Throw<ArgumentException>(() => ArtifactProfileSnapshot.Create(default, Valid())).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => ArtifactProfileSnapshot.Create(_key, null!)).ParamName.ShouldBe("options");
    }

    [Theory]
    [InlineData("version")]
    [InlineData("no-routes")]
    [InlineData("blank-backend")]
    [InlineData("no-default")]
    [InlineData("unrouted-default")]
    [InlineData("no-mutability")]
    [InlineData("undefined-mutability")]
    public void Create_WhenTheProfileIsInvalid_ThrowsInvalidOperation(string defect)
    {
        var options = Valid();
        switch (defect)
        {
            case "version":
                options.Version = default;
                break;
            case "no-routes":
                options.Routes.Clear();
                break;
            case "blank-backend":
                options.Routes[_directory] = default;
                break;
            case "no-default":
                options.DefaultDirectory = null;
                break;
            case "unrouted-default":
                options.DefaultDirectory = new ArtifactDirectoryId("elsewhere");
                break;
            case "no-mutability":
                options.AllowedMutability.Clear();
                break;
            default:
                _ = options.AllowedMutability.Add((ArtifactMutability) 99);
                break;
        }

        _ = Should.Throw<InvalidOperationException>(() => ArtifactProfileSnapshot.Create(_key, options));
    }

    [Fact]
    public void Equals_WhenContentMatches_ComparesRoutesAndMutabilityByContent()
    {
        var first = ArtifactProfileSnapshot.Create(_key, Valid());
        var second = ArtifactProfileSnapshot.Create(_key, Valid());
        var different = Valid();
        different.Routes[_directory] = new ArtifactBackendKey("other");

        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.Equals(ArtifactProfileSnapshot.Create(_key, different)).ShouldBeFalse();
        first.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Backends_WhenSeveralDirectoriesShareBackends_ListsEachDistinctBackendOnceInOrdinalOrder()
    {
        var options = Valid();
        options.Routes[new ArtifactDirectoryId("b")] = new ArtifactBackendKey("zeta");
        options.Routes[new ArtifactDirectoryId("c")] = new ArtifactBackendKey("zeta");
        options.Routes[new ArtifactDirectoryId("d")] = new ArtifactBackendKey("alpha");

        ArtifactProfileSnapshot.Create(_key, options).Backends().ShouldBe([new ArtifactBackendKey("alpha"), _backend, new ArtifactBackendKey("zeta")]);
    }

    private static ArtifactProfileOptions Valid() => new()
    {
        DefaultDirectory = _directory,
        Routes = { [_directory] = _backend },
    };
}

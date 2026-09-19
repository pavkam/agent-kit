// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookCatalogSnapshotTests
{
    [Fact]
    public void Constructor_WhenProfileKeyIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookCatalogSnapshot(
            default, new HookCatalogVersion("v1"), []));

        exception.ParamName.ShouldBe("profileKey");
    }

    [Fact]
    public void Constructor_WhenVersionIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookCatalogSnapshot(
            HookKernelTestData.Profile, default, []));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenRegistrationsContainsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookCatalogSnapshot(
            HookKernelTestData.Profile, new HookCatalogVersion("v1"), [null!]));

        exception.ParamName.ShouldBe("registrations");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsEveryProperty()
    {
        var version = new HookCatalogVersion("v1");
        var registration = HookKernelTestData.Registration();

        var snapshot = new HookCatalogSnapshot(HookKernelTestData.Profile, version, [registration]);

        snapshot.ProfileKey.ShouldBe(HookKernelTestData.Profile);
        snapshot.Version.ShouldBe(version);
        snapshot.Registrations.ShouldBe([registration]);
    }

    [Fact]
    public void Equality_WhenRegistrationsMatchByValue_InstancesAreStructurallyEqual()
    {
        var version = new HookCatalogVersion("v1");
        var registration = HookKernelTestData.Registration();

        var first = new HookCatalogSnapshot(HookKernelTestData.Profile, version, [registration]);
        var second = new HookCatalogSnapshot(HookKernelTestData.Profile, version, [registration]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}

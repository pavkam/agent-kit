// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>Verifies profile accumulation, validation, and deterministic snapshot projection.</summary>
public sealed class DurabilityProfileRegistryTests
{
    [Fact]
    public void Configure_WhenTheKeyIsDefault_ThrowsArgumentException()
    {
        var registry = new DurabilityProfileRegistry();

        var exception = Should.Throw<ArgumentException>(
            () => registry.Configure(default, Complete, replace: false));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void Configure_WhenTheCallbackIsNull_ThrowsArgumentNullException()
    {
        var registry = new DurabilityProfileRegistry();

        var exception = Should.Throw<ArgumentNullException>(
            () => registry.Configure(new DurabilityProfileKey("primary"), null!, replace: false));

        exception.ParamName.ShouldBe("configure");
    }

    [Theory]
    [InlineData("backend", "BackendKey")]
    [InlineData("journal", "JournalKey")]
    [InlineData("leases", "LeaseManagerKey")]
    [InlineData("policy", "RecoveryPolicyKey")]
    public void Configure_WhenARequiredSelectionIsUnset_ThrowsArgumentException(string omitted, string paramName)
    {
        // A half-selected profile would otherwise fail in the middle of an operation instead of at composition.
        var registry = new DurabilityProfileRegistry();

        var exception = Should.Throw<ArgumentException>(() => registry.Configure(
            new DurabilityProfileKey("primary"),
            options =>
            {
                Complete(options);
                switch (omitted)
                {
                    case "backend":
                        options.BackendKey = default;
                        break;
                    case "journal":
                        options.JournalKey = default;
                        break;
                    case "leases":
                        options.LeaseManagerKey = default;
                        break;
                    default:
                        options.RecoveryPolicyKey = default;
                        break;
                }
            },
            replace: false));

        exception.ParamName.ShouldBe(paramName);
    }

    [Fact]
    public void Configure_WhenTheVersionIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        var registry = new DurabilityProfileRegistry();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => registry.Configure(
            new DurabilityProfileKey("primary"),
            options =>
            {
                Complete(options);
                options.Version = new DurabilityProfileVersion(0);
            },
            replace: false));

        exception.ParamName.ShouldBe("Version");
    }

    [Fact]
    public void Configure_WhenAnEnabledOperationIsBlank_ThrowsArgumentException()
    {
        var registry = new DurabilityProfileRegistry();

        var exception = Should.Throw<ArgumentException>(() => registry.Configure(
            new DurabilityProfileKey("primary"),
            options =>
            {
                Complete(options);
                options.EnabledOperations.Add(default);
            },
            replace: false));

        exception.ParamName.ShouldBe("EnabledOperations");
    }

    [Fact]
    public void Configure_WhenCalledAgainWithoutReplace_RefinesTheAccumulatedOptions()
    {
        var registry = new DurabilityProfileRegistry();
        registry.Configure(new DurabilityProfileKey("primary"), Complete, replace: false);

        registry.Configure(
            new DurabilityProfileKey("primary"),
            options => options.EnabledOperations.Add(new DurableOperationName("tool.call")),
            replace: false);

        registry.TryGet(new DurabilityProfileKey("primary"), out var profile).ShouldBeTrue();
        profile!.BackendKey.ShouldBe(new DurableBackendKey("backend"));
        profile.EnabledOperations.ShouldBe([new DurableOperationName("tool.call")]);
    }

    [Fact]
    public void Configure_WhenCalledWithReplace_DiscardsTheEarlierContributions()
    {
        var registry = new DurabilityProfileRegistry();
        registry.Configure(
            new DurabilityProfileKey("primary"),
            options =>
            {
                Complete(options);
                options.EnabledOperations.Add(new DurableOperationName("tool.call"));
            },
            replace: false);

        registry.Configure(new DurabilityProfileKey("primary"), Complete, replace: true);

        registry.TryGet(new DurabilityProfileKey("primary"), out var profile).ShouldBeTrue();
        profile!.EnabledOperations.ShouldBeEmpty();
    }

    [Fact]
    public void Configure_WhenTheConfiguredProfileIsInvalid_LeavesTheEarlierProfileUnchanged()
    {
        var registry = new DurabilityProfileRegistry();
        registry.Configure(new DurabilityProfileKey("primary"), Complete, replace: false);

        _ = Should.Throw<ArgumentException>(() => registry.Configure(
            new DurabilityProfileKey("primary"),
            options =>
            {
                Complete(options);
                options.JournalKey = default;
            },
            replace: true));

        registry.TryGet(new DurabilityProfileKey("primary"), out var profile).ShouldBeTrue();
        profile!.JournalKey.ShouldBe(new DurableJournalKey("journal"));
    }

    [Fact]
    public void TryGet_WhenTheKeyIsNotRegistered_ReturnsFalse()
    {
        var registry = new DurabilityProfileRegistry();
        registry.Configure(new DurabilityProfileKey("primary"), Complete, replace: false);

        registry.TryGet(new DurabilityProfileKey("absent"), out var profile).ShouldBeFalse();

        profile.ShouldBeNull();
    }

    [Fact]
    public void TryGet_WhenTheKeyIsDefault_ReturnsFalse()
    {
        var registry = new DurabilityProfileRegistry();

        registry.TryGet(default, out var profile).ShouldBeFalse();

        profile.ShouldBeNull();
    }

    [Fact]
    public void TryGet_WhenTheProfileIsRegistered_ProjectsEverySelection()
    {
        var registry = new DurabilityProfileRegistry();
        registry.Configure(new DurabilityProfileKey("primary"), Complete, replace: false);

        registry.TryGet(new DurabilityProfileKey("primary"), out var profile).ShouldBeTrue();

        profile!.Key.ShouldBe(new DurabilityProfileKey("primary"));
        profile.Version.ShouldBe(new DurabilityProfileVersion(1));
        profile.BackendKey.ShouldBe(new DurableBackendKey("backend"));
        profile.JournalKey.ShouldBe(new DurableJournalKey("journal"));
        profile.LeaseManagerKey.ShouldBe(new DurableLeaseManagerKey("leases"));
        profile.RecoveryPolicyKey.ShouldBe(new RecoveryPolicyKey("policy"));
        profile.ConfigurationFingerprint.Value.ShouldStartWith("sha256:");
    }

    [Fact]
    public void TryGet_WhenTwoRegistriesHoldTheSameSelection_ProducesTheSameFingerprint()
    {
        var first = new DurabilityProfileRegistry();
        var second = new DurabilityProfileRegistry();
        first.Configure(new DurabilityProfileKey("primary"), Complete, replace: false);
        second.Configure(new DurabilityProfileKey("primary"), Complete, replace: false);

        _ = first.TryGet(new DurabilityProfileKey("primary"), out var left);
        _ = second.TryGet(new DurabilityProfileKey("primary"), out var right);

        right!.ConfigurationFingerprint.ShouldBe(left!.ConfigurationFingerprint);
    }

    [Fact]
    public void TryGet_WhenEnabledOperationsWereAddedInDifferentOrders_ProducesTheSameFingerprint()
    {
        // Two equivalent selections must fingerprint identically; registration order is not part of the selection.
        var first = new DurabilityProfileRegistry();
        var second = new DurabilityProfileRegistry();
        first.Configure(new DurabilityProfileKey("primary"), options =>
        {
            Complete(options);
            options.EnabledOperations.Add(new DurableOperationName("a"));
            options.EnabledOperations.Add(new DurableOperationName("b"));
        }, replace: false);
        second.Configure(new DurabilityProfileKey("primary"), options =>
        {
            Complete(options);
            options.EnabledOperations.Add(new DurableOperationName("b"));
            options.EnabledOperations.Add(new DurableOperationName("a"));
        }, replace: false);

        _ = first.TryGet(new DurabilityProfileKey("primary"), out var left);
        _ = second.TryGet(new DurabilityProfileKey("primary"), out var right);

        right!.ConfigurationFingerprint.ShouldBe(left!.ConfigurationFingerprint);
    }

    [Fact]
    public void TryGet_WhenOneSelectionDiffers_ProducesADifferentFingerprint()
    {
        var first = new DurabilityProfileRegistry();
        var second = new DurabilityProfileRegistry();
        first.Configure(new DurabilityProfileKey("primary"), Complete, replace: false);
        second.Configure(new DurabilityProfileKey("primary"), options =>
        {
            Complete(options);
            options.JournalKey = new DurableJournalKey("other");
        }, replace: false);

        _ = first.TryGet(new DurabilityProfileKey("primary"), out var left);
        _ = second.TryGet(new DurabilityProfileKey("primary"), out var right);

        right!.ConfigurationFingerprint.ShouldNotBe(left!.ConfigurationFingerprint);
    }

    [Fact]
    public void TryGet_WhenTwoProfilesAreRegistered_KeepsThemIndependent()
    {
        var registry = new DurabilityProfileRegistry();
        registry.Configure(new DurabilityProfileKey("primary"), Complete, replace: false);
        registry.Configure(new DurabilityProfileKey("secondary"), options =>
        {
            Complete(options);
            options.JournalKey = new DurableJournalKey("other");
        }, replace: false);

        _ = registry.TryGet(new DurabilityProfileKey("primary"), out var primary);
        _ = registry.TryGet(new DurabilityProfileKey("secondary"), out var secondary);

        primary!.JournalKey.ShouldBe(new DurableJournalKey("journal"));
        secondary!.JournalKey.ShouldBe(new DurableJournalKey("other"));
    }

    private static void Complete(DurabilityProfileOptions options)
    {
        options.BackendKey = new DurableBackendKey("backend");
        options.JournalKey = new DurableJournalKey("journal");
        options.LeaseManagerKey = new DurableLeaseManagerKey("leases");
        options.RecoveryPolicyKey = new RecoveryPolicyKey("policy");
    }
}

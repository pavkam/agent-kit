// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

using System.Net;

using AgentKit;

public sealed class NetworkRequestTests
{
    [Fact]
    public void Constructor_WhenResolvedAddressesDefault_ThrowsBeforeConstruction()
    {
        var action = () => new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            default, NetworkDataClassification.Public, Grant());

        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("resolvedAddresses");
    }

    [Fact]
    public void Constructor_WhenResolvedAddressesEmpty_ThrowsBeforeConstruction()
    {
        var action = () => new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            [], NetworkDataClassification.Public, Grant());

        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("resolvedAddresses");
    }

    [Fact]
    public void Constructor_WhenClassificationUndefined_ThrowsBeforeConstruction()
    {
        var action = () => new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            [Address()], (NetworkDataClassification) 99, Grant());

        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("classification");
    }

    [Fact]
    public void Constructor_WhenGrantNull_ThrowsBeforeConstruction()
    {
        var action = () => new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            [Address()], NetworkDataClassification.Public, null!);

        action.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("grant");
    }

    [Fact]
    public void RequestFingerprint_WhenHeaderAndBodySecret_UsesStableSecretFreeDigest()
    {
        var request = new NetworkRequest(
            Id(),
            NetworkMethod.Post,
            Destination(),
            new NetworkHeaderSet([new NetworkHeader("Authorization", "Bearer do-not-retain")]),
            new NetworkRequestContent("text/plain", "private payload"u8.ToArray()),
            Bounds(),
            [Address()],
            NetworkDataClassification.Confidential,
            Grant());

        var first = NetworkSecurityBinding.RequestFingerprint(request);
        var second = NetworkSecurityBinding.RequestFingerprint(request);

        first.ShouldBe(second);
        first.Value.ShouldStartWith("sha256:");
        first.Value.ShouldNotContain("do-not-retain");
        first.Value.ShouldNotContain("private payload");
    }

    [Fact]
    public void RequestResources_WhenMultipleAddresses_PreservesCanonicalRouteThenAddressOrder()
    {
        var second = new NetworkAddress(
            IPAddress.Parse("2001:db8::1"),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddMinutes(1));
        var request = new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            [Address(), second], NetworkDataClassification.Public, Grant());

        var resources = NetworkSecurityBinding.RequestResources(request);

        resources.Length.ShouldBe(3);
        resources[0].Identifier.ShouldStartWith("https://example.test:443/path?query=sha256:");
        resources[0].Identifier.ShouldNotContain("q=1");
        resources[1].Identifier.ShouldBe("192.0.2.1:443");
        resources[2].Identifier.ShouldBe("[2001:db8::1]:443");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsGrant()
    {
        var grant = Grant();
        var request = new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            [Address()], NetworkDataClassification.Public, grant);
        request.Grant.ShouldBeSameAs(grant);
    }

    [Fact]
    public void Equals_WhenResolvedAddressesAreDifferentArrayInstancesWithTheSameElements_AreEqual()
    {
        // Two independently-built ImmutableArray<NetworkAddress> instances with identical elements
        // must compare equal here: the documented "structural equality over its fields" contract
        // must not fall back to ImmutableArray<T>'s own reference equality.
        var first = new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            [Address()], NetworkDataClassification.Public, Grant());
        var second = new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            [Address()], NetworkDataClassification.Public, Grant());

        first.ResolvedAddresses.ShouldNotBeSameAs(second.ResolvedAddresses);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenResolvedAddressesDiffer_AreNotEqual()
    {
        var other = new NetworkAddress(
            IPAddress.Parse("192.0.2.2"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
        var first = new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            [Address()], NetworkDataClassification.Public, Grant());
        var second = new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            [other], NetworkDataClassification.Public, Grant());

        first.ShouldNotBe(second);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new NetworkRequest(
            Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
            [Address()], NetworkDataClassification.Public, Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static NetworkOperationId Id() => new(
        Guid.Parse("10000000-0000-0000-0000-000000000001"));

    private static NetworkDestination Destination() => new(
        "https", new NormalizedHost("example.test"), 443, new NetworkRoute("/path?q=1"));

    private static NetworkBounds Bounds() => new(
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 4_096, 3);

    private static NetworkAddress Address() => new(
        IPAddress.Parse("192.0.2.1"),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(1));

    private static SecurityGrant Grant() => new(
        new GrantId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
        new SecurityRequestId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
        new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            null,
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")), null)),
        TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human),
        new ComponentId("test"),
        SecurityOperationKind.Network,
        SecurityEffect.Egress,
        [new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, "test")],
        new InputFingerprint("sha256:test"),
        new SecurityPolicyVersion(1),
        new SecurityRevocationVersion(1),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.MaxValue,
        1);
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

using System.Net;

using AgentKit;

/// <summary>Verifies NetworkSecurityBinding behavior and contracts.</summary>
public sealed class NetworkSecurityBindingTests
{
    [Fact]
    public void ResolutionResource_WhenDestinationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => NetworkSecurityBinding.ResolutionResource(null!)).ParamName.ShouldBe("destination");

    [Fact]
    public void ResolutionResource_WhenDestinationIsValid_ReturnsOriginResource()
    {
        var destination = Destination();
        var resource = NetworkSecurityBinding.ResolutionResource(destination);
        resource.Identifier.ShouldBe("https://example.test:443");
    }

    [Fact]
    public void ResolutionFingerprint_WhenRequestIsNull_ThrowsExactParameter()
    {
        NetworkResolutionRequest? request = null;
        Should.Throw<ArgumentNullException>(() => NetworkSecurityBinding.ResolutionFingerprint(request!)).ParamName.ShouldBe("request");
    }

    [Fact]
    public void ResolutionFingerprint_WhenRequestIsValid_MatchesComponentOverload()
    {
        var id = Id();
        var destination = Destination();
        var bounds = Bounds();
        var request = new NetworkResolutionRequest(id, destination, bounds, Grant());

        var fromRequest = NetworkSecurityBinding.ResolutionFingerprint(request);
        var fromComponents = NetworkSecurityBinding.ResolutionFingerprint(id, destination, bounds);

        fromRequest.ShouldBe(fromComponents);
    }

    [Fact]
    public void ResolutionFingerprint_WhenDestinationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => NetworkSecurityBinding.ResolutionFingerprint(Id(), null!, Bounds())).ParamName.ShouldBe("destination");

    [Fact]
    public void ResolutionFingerprint_WhenBoundsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => NetworkSecurityBinding.ResolutionFingerprint(Id(), Destination(), null!)).ParamName.ShouldBe("bounds");

    [Fact]
    public void RequestResourceIdentifier_WhenRouteHasNoQuery_UsesDestinationToString()
    {
        var destination = new NetworkDestination("https", new NormalizedHost("example.test"), 443, new NetworkRoute("/path"));
        var resources = NetworkSecurityBinding.RequestResources(destination, [Address()]);
        resources[0].Identifier.ShouldBe(destination.ToString());
    }

    [Fact]
    public void ResolutionResource_WhenHostIsIPv6Literal_BracketsTheHost()
    {
        var destination = new NetworkDestination("https", new NormalizedHost("::1"), 443, new NetworkRoute("/path"));

        var resource = NetworkSecurityBinding.ResolutionResource(destination);

        resource.Identifier.ShouldBe("https://[::1]:443");
    }

    [Fact]
    public void RequestResourceIdentifier_WhenHostIsIPv6LiteralAndRouteHasAQuery_UsesTheSameBracketedAuthorityAsResolutionResource()
    {
        var destination = new NetworkDestination("https", new NormalizedHost("::1"), 443, new NetworkRoute("/path?q=1"));

        var resolution = NetworkSecurityBinding.ResolutionResource(destination);
        var request = NetworkSecurityBinding.RequestResources(destination, [Address()]);

        request[0].Identifier.ShouldStartWith("https://[::1]:443/path?query=");
        // Every identifier for the same IPv6 destination must agree on the bracketed authority form,
        // regardless of which operation or route shape produced it.
        resolution.Identifier.ShouldBe("https://[::1]:443");
        request[0].Identifier.ShouldStartWith(resolution.Identifier);
    }

    private static NetworkOperationId Id() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static NetworkDestination Destination() => new("https", new NormalizedHost("example.test"), 443, new NetworkRoute("/path?q=1"));
    private static NetworkBounds Bounds() => new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 4_096, 3);
    private static NetworkAddress Address() => new(IPAddress.Parse("192.0.2.1"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));

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

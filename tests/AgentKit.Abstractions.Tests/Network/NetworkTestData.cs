// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

using System.Net;

internal static class NetworkTestData
{
    public static NetworkOperationId Id() => new(Guid.Parse("10000000-0000-0000-0000-000000000001"));

    public static NetworkDestination Destination() => new(
        "https", new NormalizedHost("example.test"), 443, new NetworkRoute("/path?q=1"));

    public static NetworkBounds Bounds() => new(
        TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), 4_096, 3);

    public static NetworkAddress Address() => new(
        IPAddress.Parse("192.0.2.1"),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(1));

    public static SecurityGrant Grant() => new(
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

    public static NetworkRequest Request() => new(
        Id(), NetworkMethod.Get, Destination(), NetworkHeaderSet.Empty, null, Bounds(),
        [Address()], NetworkDataClassification.Public, Grant());

    public static NetworkResolutionRequest ResolutionRequest() => new(Id(), Destination(), Bounds(), Grant());

    public sealed class FakeNetworkResponse: INetworkResponse
    {
        public FakeNetworkResponse(NetworkResponseMetadata metadata, Stream? content = null)
        {
            Metadata = metadata;
            Content = content ?? Stream.Null;
        }

        public NetworkResponseMetadata Metadata { get; }

        public Stream Content { get; }

        public NetworkEgressEvidence? EgressEvidence { get; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

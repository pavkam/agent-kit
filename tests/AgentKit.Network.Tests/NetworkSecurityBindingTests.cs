// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.Tests;



/// <summary>Verifies NetworkSecurityBinding behavior and contracts.</summary>
public sealed class NetworkSecurityBindingTests
{
    [Fact]
    public void RequestFingerprint_WhenHeadersAndBodySensitive_ContainsNoRawValues()
    {
        var request = new NetworkRequest(new NetworkOperationId(Guid.Parse("50000000-0000-0000-0000-000000000005")), NetworkMethod.Post, Destination(443), new NetworkHeaderSet([new NetworkHeader("Authorization", "Bearer secret")]), new NetworkRequestContent("text/plain", "private body"u8.ToArray()), Bounds(), [Address()], NetworkDataClassification.Confidential, TestSecurity.Grant());
        var fingerprint = NetworkSecurityBinding.RequestFingerprint(request);
        fingerprint.Value.ShouldStartWith("sha256:");
        fingerprint.Value.ShouldNotContain("secret");
        fingerprint.Value.ShouldNotContain("private body");
    }

    private static NetworkDestination Destination(int port) => new("http", new NormalizedHost("127.0.0.1"), port, NetworkRoute.Root);
    private static NetworkBounds Bounds(TimeSpan? responseTimeout = null, long maximumResponseBytes = 1_024) => new(TimeSpan.FromSeconds(2), responseTimeout ?? TimeSpan.FromSeconds(2), maximumResponseBytes, 3);
    private static NetworkAddress Address() => new(IPAddress.Loopback, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1));
}

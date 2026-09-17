// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Net;

/// <summary>Creates exact resources and secret-free fingerprints for network authority.</summary>
public static class NetworkSecurityBinding
{
    /// <summary>Creates the canonical origin resource observed during resolution.</summary>
    /// <param name="destination">The canonical destination.</param>
    /// <returns>The exact scheme, host, and port resource.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    public static ProtectedResource ResolutionResource(NetworkDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, destination.Authority);
    }

    /// <summary>Creates destination and resolved-address resources for a connection.</summary>
    /// <param name="request">The complete validated request.</param>
    /// <returns>The routed destination followed by every eligible address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static ImmutableArray<ProtectedResource> RequestResources(NetworkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RequestResources(request.Destination, request.ResolvedAddresses);
    }

    /// <summary>Creates destination and resolved-address resources before a send grant exists.</summary>
    /// <param name="destination">The canonical routed destination.</param>
    /// <param name="resolvedAddresses">The exact non-empty resolved-address set.</param>
    /// <returns>The routed destination followed by every eligible address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="resolvedAddresses"/> is default or empty.</exception>
    public static ImmutableArray<ProtectedResource> RequestResources(
        NetworkDestination destination,
        ImmutableArray<NetworkAddress> resolvedAddresses)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfDefaultOrEmpty(resolvedAddresses);
        return
        [
            new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, RequestResourceIdentifier(destination)),
            .. resolvedAddresses.Select(address => new ProtectedResource(
                ProtectedResourceKind.NetworkEndpoint,
                $"{AddressHost(address.Address)}:{destination.Port}")),
        ];
    }

    /// <summary>Computes the exact resolution fingerprint.</summary>
    /// <param name="request">The complete validated resolution request.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint ResolutionFingerprint(NetworkResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolutionFingerprint(request.Id, request.Destination, request.Bounds);
    }

    /// <summary>Computes exact resolution intent before a resolution grant exists.</summary>
    /// <param name="id">The resolution operation identity.</param>
    /// <param name="destination">The canonical destination.</param>
    /// <param name="bounds">The enforced network bounds.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> or <paramref name="bounds"/> is null.</exception>
    public static InputFingerprint ResolutionFingerprint(
        NetworkOperationId id,
        NetworkDestination destination,
        NetworkBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(bounds);
        return Hash(new
        {
            operation = "network-resolve-v1",
            operationId = id.ToString(),
            destination = destination.ToString(),
            connectTimeoutTicks = bounds.ConnectTimeout.Ticks,
        });
    }

    /// <summary>Computes the exact request fingerprint without retaining header values or body bytes.</summary>
    /// <param name="request">The complete validated request.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public static InputFingerprint RequestFingerprint(NetworkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RequestFingerprint(
            request.Id,
            request.Method,
            request.Destination,
            request.Headers,
            request.Content,
            request.Bounds,
            request.ResolvedAddresses,
            request.Classification);
    }

    /// <summary>Computes exact secret-free send intent before a send grant exists.</summary>
    /// <param name="id">The send operation identity.</param>
    /// <param name="method">The exact request method.</param>
    /// <param name="destination">The canonical routed destination.</param>
    /// <param name="headers">The exact ordered request headers.</param>
    /// <param name="content">The optional exact body content.</param>
    /// <param name="bounds">The enforced network bounds.</param>
    /// <param name="resolvedAddresses">The exact non-empty resolved-address set.</param>
    /// <param name="classification">The maximum sensitivity intentionally transmitted.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint containing only hashes of header values and body bytes.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="destination"/>, <paramref name="headers"/>, or <paramref name="bounds"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="resolvedAddresses"/> is default or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="classification"/> is undefined.</exception>
    public static InputFingerprint RequestFingerprint(
        NetworkOperationId id,
        NetworkMethod method,
        NetworkDestination destination,
        NetworkHeaderSet headers,
        NetworkRequestContent? content,
        NetworkBounds bounds,
        ImmutableArray<NetworkAddress> resolvedAddresses,
        NetworkDataClassification classification)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentException.ThrowIfDefaultOrEmpty(resolvedAddresses);
        ArgumentOutOfRangeException.ThrowIfUndefined(classification);
        return Hash(new
        {
            operation = "network-send-v1",
            operationId = id.ToString(),
            method = method.Value,
            destination = destination.ToString(),
            headers = headers.Headers.Select(static header => new
            {
                name = header.Name.ToLowerInvariant(),
                valueFingerprint = ProcessSecurityBinding.FingerprintText(header.Value),
            }),
            contentType = content?.ContentType,
            contentFingerprint = content is null
                ? null
                : ProcessSecurityBinding.FingerprintBytes(content.Body.Span).Value,
            addresses = resolvedAddresses.Select(static address => new
            {
                value = address.Address.ToString(),
                address.ResolvedAt,
                address.ExpiresAt,
            }),
            classification,
            connectTimeoutTicks = bounds.ConnectTimeout.Ticks,
            responseTimeoutTicks = bounds.ResponseTimeout.Ticks,
            bounds.MaximumResponseBytes,
            bounds.MaximumRedirects,
        });
    }

    private static InputFingerprint Hash<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        return new InputFingerprint($"sha256:{Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()}");
    }

    private static string RequestResourceIdentifier(NetworkDestination destination)
    {
        var route = destination.Route.Value;
        var queryStart = route.IndexOf('?');
        if (queryStart < 0)
        {
            return destination.ToString();
        }

        var path = route[..queryStart];
        var queryFingerprint = ProcessSecurityBinding.FingerprintText(route[(queryStart + 1)..]);
        return $"{destination.Authority}{path}?query={queryFingerprint}";
    }

    private static string AddressHost(IPAddress address) =>
        address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{address}]"
            : address.ToString();
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Creates the exact, secret-free resources and fingerprint a provider-egress grant binds.</summary>
/// <remarks>
/// The fingerprint binds the provider, API family, service surface, endpoint and credential profile references,
/// model identity and revision, attempt, streaming mode, method, canonical destination, payload hash and length,
/// header names, declared classification, deadline, and response bound. It never contains a credential, a header
/// value, or body bytes: the credential is identified only by its profile key and version, and the payload only by its
/// SHA-256 hash. Any change to a bound value therefore requires a fresh authorization.
/// </remarks>
internal static class ProviderEgressBinding
{
    /// <summary>The enforcement audience that consumes provider-egress grants.</summary>
    internal static readonly ComponentId Audience = new("agentkit.providers.egress");

    /// <summary>Creates the single protected destination resource for a provider-egress grant.</summary>
    /// <param name="destination">The canonical routed destination.</param>
    /// <returns>The scheme, host, port, and path, with the query reduced to its fingerprint.</returns>
    internal static ImmutableArray<ProtectedResource> Resources(NetworkDestination destination)
    {
        Debug.Assert(destination is not null, "The caller builds the canonical destination before binding it.");

        var route = destination.Route.Value;
        var queryStart = route.IndexOf('?');
        var identifier = queryStart < 0
            ? destination.ToString()
            : $"{destination.Authority}{route[..queryStart]}?query={ProcessSecurityBinding.FingerprintText(route[(queryStart + 1)..])}";
        return [new ProtectedResource(ProtectedResourceKind.NetworkEndpoint, identifier)];
    }

    /// <summary>Computes the exact provider-egress fingerprint without retaining secrets or content.</summary>
    /// <param name="request">The attempt identity.</param>
    /// <param name="method">The exact method.</param>
    /// <param name="destination">The canonical routed destination.</param>
    /// <param name="headers">The ordered request headers; only their names are bound here.</param>
    /// <param name="content">The frozen request body.</param>
    /// <param name="classification">The declared payload classification.</param>
    /// <param name="maximumResponseBytes">The streaming response bound.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    internal static InputFingerprint Fingerprint(
        ProviderEgressRequest request,
        NetworkMethod method,
        NetworkDestination destination,
        NetworkHeaderSet headers,
        NetworkRequestContent? content,
        NetworkDataClassification classification,
        long maximumResponseBytes)
    {
        Debug.Assert(request is not null, "The caller supplies the attempt identity.");
        Debug.Assert(destination is not null, "The caller builds the canonical destination before binding it.");
        Debug.Assert(headers is not null, "The caller supplies the ordered header set.");

        var binding = request.Binding;
        var payload = new
        {
            operation = "provider-egress-v1",
            kind = request.Kind.ToString(),
            provider = request.ProviderId.Value,
            apiFamily = request.ApiFamily.Value,
            serviceSurface = request.ServiceSurface.Value,
            endpoint = request.EndpointId.Value,
            endpointProfile = binding is null
                ? null
                : new { key = binding.Endpoint.Key.Value, version = binding.Endpoint.Version.Value },
            credentialProfile = binding is null
                ? null
                : new { key = binding.Credential.Key.Value, version = binding.Credential.Version.Value },
            model = request.ModelId.Value,
            deployment = request.DeploymentId?.Value,
            revision = request.ModelRevision,
            attempt = request.Attempt,
            streaming = request.Streaming,
            method = method.Value,
            destination = Resources(destination)[0].Identifier,
            contentType = content?.ContentType,
            contentFingerprint = content?.BodyFingerprint.Value,
            contentBytes = content?.Body.Length ?? 0,
            headerNames = headers.Headers
                .Select(static header => header.Name.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            classification = classification.ToString(),
            deadlineTicks = request.Deadline.UtcTicks,
            maximumResponseBytes,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        return new InputFingerprint($"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}");
    }

    /// <summary>Creates the exact enforcement evidence the adapter presents when it consumes its own grant.</summary>
    /// <param name="grant">The grant issued for this attempt.</param>
    /// <param name="resources">The grant's protected resources.</param>
    /// <param name="fingerprint">The grant's input fingerprint.</param>
    /// <returns>Enforcement evidence naming the provider-egress audience.</returns>
    internal static SecurityEnforcementRequest Enforcement(
        SecurityGrant grant,
        ImmutableArray<ProtectedResource> resources,
        InputFingerprint fingerprint)
    {
        Debug.Assert(grant is not null, "The caller consumes a grant issued by the authority.");
        return new SecurityEnforcementRequest(
            grant.Scope,
            grant.Identity,
            grant.Authorization,
            Audience,
            SecurityOperationKind.Network,
            SecurityEffect.Egress,
            resources,
            fingerprint,
            grant.RevocationVersion);
    }

    /// <summary>Determines whether a consumption result authorizes this new exact provider-egress effect.</summary>
    /// <param name="consumption">The atomic grant-store result.</param>
    /// <param name="grant">The grant presented to the store.</param>
    /// <param name="enforcement">The evidence presented to the store.</param>
    /// <param name="intent">The freshly generated attempt identity.</param>
    /// <returns><see langword="true"/> only for a newly consumed use with a complete matching receipt.</returns>
    internal static bool IsFreshExact(
        GrantConsumptionResult consumption,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent)
    {
        Debug.Assert(consumption is not null, "The grant store always returns a result.");
        Debug.Assert(grant is not null, "The caller presents the grant it consumed.");
        Debug.Assert(enforcement is not null, "The caller presents the evidence it consumed.");
        Debug.Assert(intent is not null, "The caller presents the fresh intent it consumed.");

        return consumption.Status is GrantConsumptionStatus.Consumed
            && consumption.IntentReceipt is { } receipt
            && receipt.IntentId == intent.Id
            && receipt.GrantId == grant.Id
            && receipt.RequestId == grant.RequestId
            && receipt.RequiredFence == intent.RequiredFence
            && receipt.EffectFingerprint == SecurityEnforcementBinding.Fingerprint(enforcement, intent)
            && HasExactEnforcement(receipt.Enforcement, enforcement);
    }

    /// <summary>Returns a stable non-content denial message for a consumption that failed fresh exact validation.</summary>
    /// <param name="consumption">The failed consumption result.</param>
    /// <returns>The store message, or a fixed message when a consumed result lacks its receipt.</returns>
    internal static string DenialMessage(GrantConsumptionResult consumption)
    {
        Debug.Assert(consumption is not null, "The grant store always returns a result.");
        return consumption.Status is GrantConsumptionStatus.Consumed
            ? "The grant store did not retain a fresh exact enforcement-intent receipt."
            : consumption.SafeMessage;
    }

    private static bool HasExactEnforcement(SecurityEnforcementRequest actual, SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "A receipt comparison receives enforcement retained by a validated receipt.");
        Debug.Assert(expected is not null, "A receipt comparison receives enforcement created by this boundary.");
        return actual.Scope == expected.Scope
            && actual.Identity == expected.Identity
            && actual.Authorization == expected.Authorization
            && actual.Audience == expected.Audience
            && actual.Kind == expected.Kind
            && actual.Effect == expected.Effect
            && actual.InputFingerprint == expected.InputFingerprint
            && actual.RevocationVersion == expected.RevocationVersion
            && actual.Resources.SequenceEqual(expected.Resources);
    }
}

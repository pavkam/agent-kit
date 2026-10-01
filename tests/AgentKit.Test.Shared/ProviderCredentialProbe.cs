// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using AgentKit.Providers.Credentials;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Releases one credential through a registered <see cref="IProviderCredentialSource"/> exactly as provider egress
/// would: it asks the granting authority for a credential-read grant bound to the profile snapshots, resolves a lease,
/// applies it to a recording target, and disposes it, so tests can observe the header a composition would send.
/// </summary>
public static class ProviderCredentialProbe
{
    /// <summary>The attempt deadline offset the probe requests grants for.</summary>
    private static readonly TimeSpan _deadlineOffset = TimeSpan.FromMinutes(1);

    /// <summary>Resolves the source registered under <paramref name="sourceKey"/> and probes it.</summary>
    /// <param name="services">A provider composed with <c>AddProviderEgressTestServices</c>.</param>
    /// <param name="sourceKey">The credential source key to resolve.</param>
    /// <param name="scheme">The branded API-key header shape, or null for the bearer default.</param>
    /// <param name="utcNow">The instant the target reports, or null for a fixed 2025-06-01 instant.</param>
    /// <param name="cancellationToken">A token used to cancel the probe.</param>
    /// <returns>The probe outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static ValueTask<ProviderCredentialProbeResult> ProbeAsync(
        IServiceProvider services,
        ProviderCredentialSourceKey sourceKey,
        ProviderAuthorizationScheme? scheme = null,
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        return ProbeAsync(
            services.GetRequiredKeyedService<IProviderCredentialSource>(sourceKey),
            services.GetRequiredService<GrantingSecurityAuthority>(),
            scheme,
            utcNow,
            cancellationToken);
    }

    /// <summary>Probes <paramref name="source"/> with a grant issued by <paramref name="authority"/>.</summary>
    /// <param name="source">The source to probe.</param>
    /// <param name="authority">The authority whose grant store the source's gate consumes from.</param>
    /// <param name="scheme">The branded API-key header shape, or null for the bearer default.</param>
    /// <param name="utcNow">The instant the target reports, or null for a fixed 2025-06-01 instant.</param>
    /// <param name="cancellationToken">A token used to cancel the probe.</param>
    /// <returns>The probe outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="authority"/> is null.</exception>
    public static async ValueTask<ProviderCredentialProbeResult> ProbeAsync(
        IProviderCredentialSource source,
        GrantingSecurityAuthority authority,
        ProviderAuthorizationScheme? scheme = null,
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(authority);

        var request = await CreateAuthorizedRequestAsync(
            source,
            authority,
            utcNow ?? new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero),
            cancellationToken).ConfigureAwait(false);
        return await ResolveAndApplyAsync(source, request, scheme, utcNow, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds a request for <paramref name="source"/> carrying a real single-use grant issued by <paramref name="authority"/>.</summary>
    /// <param name="source">The source whose key and audience the grant is bound to.</param>
    /// <param name="authority">The authority that issues and registers the grant.</param>
    /// <param name="now">The instant the request's deadline is relative to.</param>
    /// <param name="cancellationToken">A token used to cancel issuance.</param>
    /// <returns>A request whose grant matches its snapshots exactly.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="authority"/> is null.</exception>
    public static async ValueTask<ProviderCredentialResolutionRequest> CreateAuthorizedRequestAsync(
        IProviderCredentialSource source,
        GrantingSecurityAuthority authority,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(authority);

        var request = CreateRequest(source, now);
        var decision = await authority.AuthorizeAsync(
            new SecurityRequest(
                new SecurityRequestId(Guid.NewGuid()),
                ProviderEgressHarness.Operation.Authorization.Scope,
                toolCallId: null,
                ProviderEgressHarness.Operation.Identity,
                ProviderEgressHarness.Operation.Authorization,
                source.SecurityAudience,
                ProviderCredentialReadBinding.OperationKind,
                ProviderCredentialReadBinding.Effect,
                ProviderCredentialReadBinding.Resources(request.Credential),
                ProviderCredentialReadBinding.Fingerprint(request.Endpoint, request.Credential, request.Attempt, request.Deadline),
                request.Deadline),
            cancellationToken).ConfigureAwait(false);
        return request with { CredentialGrant = ((SecurityAllowed) decision).Grant };
    }

    /// <summary>Builds the secret-free request a probe or a negative test presents to a source.</summary>
    /// <param name="source">The source whose key and audience the snapshots name.</param>
    /// <param name="now">The instant the request's deadline is relative to.</param>
    /// <returns>A request whose grant is a placeholder the caller replaces.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static ProviderCredentialResolutionRequest CreateRequest(IProviderCredentialSource source, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);

        var provider = new ProviderId("probe-provider");
        var surface = new ProviderServiceSurfaceId("probe-surface");
        var endpoint = new ProviderEndpointProfileSnapshot(
            StaticProviderProfileRuntimeSelector.Endpoint,
            provider,
            surface,
            new ProviderEndpointId("probe-endpoint"),
            new Uri("https://api.example.test/"),
            apiVersion: null,
            new ContentHash("endpoint:probe"),
            ExtensionData.Empty);
        var credential = new ProviderCredentialProfileSnapshot(
            StaticProviderProfileRuntimeSelector.Credential,
            provider,
            surface,
            source.Key,
            new ProviderAccountId("probe-account"),
            TimeSpan.Zero,
            new ContentHash("credential:probe"),
            ExtensionData.Empty);
        return new ProviderCredentialResolutionRequest(
            endpoint,
            credential,
            ProviderEgressHarness.Operation,
            attempt: 1,
            now.Add(_deadlineOffset),
            Placeholder.Grant);
    }

    /// <summary>Resolves a lease from <paramref name="request"/> and applies it to a recording target.</summary>
    /// <param name="source">The source to resolve from.</param>
    /// <param name="request">The request carrying the grant to present.</param>
    /// <param name="scheme">The branded API-key header shape, or null for the bearer default.</param>
    /// <param name="utcNow">The instant the target reports, or null for a fixed 2025-06-01 instant.</param>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>The probe outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="request"/> is null.</exception>
    public static async ValueTask<ProviderCredentialProbeResult> ResolveAndApplyAsync(
        IProviderCredentialSource source,
        ProviderCredentialResolutionRequest request,
        ProviderAuthorizationScheme? scheme = null,
        DateTimeOffset? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        var resolution = await source.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        var target = new RecordingAuthenticationTarget(scheme, utcNow);
        if (resolution is not ProviderCredentialResolved resolved)
        {
            return new ProviderCredentialProbeResult(resolution, target.Headers, ApplyFailure: null, request.CredentialGrant);
        }

        await using (resolved.Credential.ConfigureAwait(false))
        {
            var failure = await resolved.Credential.ApplyAsync(target, cancellationToken).ConfigureAwait(false);
            return new ProviderCredentialProbeResult(resolution, target.Headers, failure, request.CredentialGrant);
        }
    }

    private static class Placeholder
    {
        internal static SecurityGrant Grant { get; } = Create();

        private static SecurityGrant Create()
        {
            var operation = ProviderEgressHarness.Operation;
            return new SecurityGrant(
                new GrantId(Guid.Parse("a1000000-0000-0000-0000-000000000001")),
                new SecurityRequestId(Guid.Parse("a1000000-0000-0000-0000-000000000002")),
                operation.Authorization.Scope,
                operation.Identity,
                operation.Authorization,
                new ComponentId("placeholder"),
                SecurityOperationKind.StateRead,
                SecurityEffect.Observe,
                [new ProtectedResource(ProtectedResourceKind.ApplicationState, "placeholder")],
                new InputFingerprint("sha256:placeholder"),
                new SecurityPolicyVersion(1),
                new SecurityRevocationVersion(1),
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch.AddDays(1),
                1);
        }
    }
}

/// <summary>The observable outcome of one credential probe.</summary>
/// <param name="Resolution">The source's resolution result.</param>
/// <param name="Headers">The headers the applied lease set, empty when nothing was applied.</param>
/// <param name="ApplyFailure">The lease's authentication failure, or null when applied or unavailable.</param>
/// <param name="Grant">The credential-read grant the source was given.</param>
public sealed record ProviderCredentialProbeResult(
    ProviderCredentialResolutionResult Resolution,
    IReadOnlyDictionary<string, string> Headers,
    ProviderFailure? ApplyFailure,
    SecurityGrant Grant);

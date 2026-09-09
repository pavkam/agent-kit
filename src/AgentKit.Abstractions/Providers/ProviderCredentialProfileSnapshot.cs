// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one immutable, secret-free credential-profile publication.</summary>
/// <remarks>This is configuration evidence, not a credential, credential-source activation, profile-pairing proof, or authority grant.</remarks>
public sealed record ProviderCredentialProfileSnapshot
{
    /// <summary>Initializes one exact credential-profile snapshot.</summary>
    /// <param name="reference">The non-null exact credential-profile reference.</param>
    /// <param name="providerId">The nondefault provider identity.</param>
    /// <param name="serviceSurface">The nondefault provider service surface.</param>
    /// <param name="sourceKey">The nondefault credential-source selection.</param>
    /// <param name="accountId">The optional nondefault provider account identity.</param>
    /// <param name="refreshSkew">The nonnegative refresh skew retained for later runtime use.</param>
    /// <param name="configurationFingerprint">The nondefault captured configuration fingerprint.</param>
    /// <param name="extensions">The non-null immutable compatible evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity/fingerprint is default, refresh skew is negative, or a present account is default.</exception>
    public ProviderCredentialProfileSnapshot(
        ProviderCredentialProfileReference reference,
        ProviderId providerId,
        ProviderServiceSurfaceId serviceSurface,
        ProviderCredentialSourceKey sourceKey,
        ProviderAccountId? accountId,
        TimeSpan refreshSkew,
        ContentHash configurationFingerprint,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfEqual(providerId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(serviceSurface, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sourceKey, default);
        if (accountId is { } value)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(accountId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(refreshSkew, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfEqual(configurationFingerprint, default);
        ArgumentNullException.ThrowIfNull(extensions);
        Reference = reference;
        ProviderId = providerId;
        ServiceSurface = serviceSurface;
        SourceKey = sourceKey;
        AccountId = accountId;
        RefreshSkew = refreshSkew;
        ConfigurationFingerprint = configurationFingerprint;
        Extensions = extensions;
    }

    /// <summary>Gets the exact profile reference.</summary><value>A non-null exact reference.</value>
    public ProviderCredentialProfileReference Reference { get; }
    /// <summary>Gets the configured provider.</summary><value>A nondefault provider identity.</value>
    public ProviderId ProviderId { get; }
    /// <summary>Gets the configured service surface.</summary><value>A nondefault service-surface identity.</value>
    public ProviderServiceSurfaceId ServiceSurface { get; }
    /// <summary>Gets the credential-source selection.</summary><value>A nondefault source key and never credential material.</value>
    public ProviderCredentialSourceKey SourceKey { get; }
    /// <summary>Gets the optional account evidence.</summary><value>A nondefault account identity when present; otherwise null.</value>
    public ProviderAccountId? AccountId { get; }
    /// <summary>Gets the refresh skew retained for later runtime use.</summary><value>A nonnegative duration.</value>
    public TimeSpan RefreshSkew { get; }
    /// <summary>Gets the captured configuration fingerprint.</summary><value>A nondefault hash retained without verification.</value>
    public ContentHash ConfigurationFingerprint { get; }
    /// <summary>Gets compatible immutable extension evidence.</summary><value>A non-null immutable bag.</value>
    public ExtensionData Extensions { get; }
}

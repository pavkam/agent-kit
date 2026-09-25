// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Resolves profile-bound credentials and optional endpoint overrides for one provider attempt.</summary>
public static class ProviderProfileAttemptBinding
{
    /// <summary>
    /// Selects a runtime lease when <paramref name="binding"/> is configured and returns the credential source to use.
    /// </summary>
    /// <param name="binding">The descriptor binding, when present.</param>
    /// <param name="operation">The protected operation for profile selection.</param>
    /// <param name="fallbackCredentials">The legacy credential source used when no binding is configured.</param>
    /// <param name="profileSelector">The optional profile runtime selector.</param>
    /// <param name="providerId">The provider identity used in typed failures.</param>
    /// <param name="cancellationToken">A token used to cancel selection.</param>
    /// <returns>A successful resolution or a typed failure.</returns>
    /// <summary>Resolves the credential source for one attempt.</summary>
    public static async ValueTask<ProfileCredentialResolution> ResolveCredentialSourceAsync(
        ProviderOperationBinding? binding,
        ProtectedSemanticOperationContext? operation,
        IProviderCredentialSource fallbackCredentials,
        IProviderProfileRuntimeSelector? profileSelector,
        ProviderId providerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fallbackCredentials);

        if (binding is null)
        {
            return ProfileCredentialResolution.FromSource(fallbackCredentials, endpointBaseAddress: null, lease: null);
        }

        if (profileSelector is null)
        {
            return ProfileCredentialResolution.FromFailure(new ProviderFailure(
                ProviderFailureKind.InvalidRequest,
                providerId,
                null,
                null,
                null,
                null,
                "The model descriptor requires profile binding but no profile runtime selector is registered.",
                null,
                ExtensionData.Empty));
        }

        if (operation is null)
        {
            return ProfileCredentialResolution.FromFailure(new ProviderFailure(
                ProviderFailureKind.InvalidRequest,
                providerId,
                null,
                null,
                null,
                null,
                "The model descriptor requires profile binding but the request did not capture a protected operation context.",
                null,
                ExtensionData.Empty));
        }

        var selection = await profileSelector
            .SelectAsync(binding, operation, cancellationToken)
            .ConfigureAwait(false);

        if (selection is ProviderProfileRuntimeUnavailable unavailable)
        {
            return ProfileCredentialResolution.FromFailure(unavailable.Failure);
        }

        var selected = (ProviderProfileRuntimeSelected) selection;
        return ProfileCredentialResolution.FromSource(
            selected.Runtime.CredentialSource,
            selected.Runtime.Endpoint.BaseAddress,
            selected.Runtime);
    }

    /// <summary>Normalizes a base address for RFC 3986 relative resolution.</summary>
    /// <summary>Normalizes a base address for relative URI resolution.</summary>
    public static Uri NormalizeBaseAddress(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        return baseAddress.AbsoluteUri.EndsWith('/')
            ? baseAddress
            : new Uri(baseAddress.AbsoluteUri + "/");
    }

    /// <summary>Credential resolution outcome for one profile-bound attempt.</summary>
    public readonly struct ProfileCredentialResolution
    {
        private ProfileCredentialResolution(
            IProviderCredentialSource? credentialSource,
            Uri? endpointBaseAddress,
            IProviderProfileRuntimeLease? lease,
            ProviderFailure? failure)
        {
            CredentialSource = credentialSource;
            EndpointBaseAddress = endpointBaseAddress;
            Lease = lease;
            Failure = failure;
        }

        /// <summary>Gets the resolved credential source when successful.</summary>
        public IProviderCredentialSource? CredentialSource { get; }

        /// <summary>Gets the endpoint base address override when profile binding supplied one.</summary>
        public Uri? EndpointBaseAddress { get; }

        /// <summary>Gets the runtime lease that must be disposed after the attempt.</summary>
        public IProviderProfileRuntimeLease? Lease { get; }

        /// <summary>Gets the typed failure when resolution did not succeed.</summary>
        public ProviderFailure? Failure { get; }

        /// <summary>Gets whether credential resolution succeeded.</summary>
        public bool IsSuccess => Failure is null && CredentialSource is not null;

        /// <summary>Creates a successful resolution.</summary>
        /// <param name="credentialSource">The credential source to use.</param>
        /// <param name="endpointBaseAddress">The optional endpoint override.</param>
        /// <param name="lease">The optional runtime lease to dispose.</param>
        /// <returns>A successful resolution value.</returns>
        public static ProfileCredentialResolution FromSource(
            IProviderCredentialSource credentialSource,
            Uri? endpointBaseAddress,
            IProviderProfileRuntimeLease? lease) =>
            new(credentialSource, endpointBaseAddress, lease, failure: null);

        /// <summary>Creates a failed resolution.</summary>
        /// <param name="failure">The typed failure.</param>
        /// <returns>A failed resolution value.</returns>
        public static ProfileCredentialResolution FromFailure(ProviderFailure failure) =>
            new(null, null, null, failure);
    }
}

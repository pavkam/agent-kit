// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Selects the profile runtime and optional endpoint override for one provider attempt.</summary>
/// <remarks>
/// Selection captures only the descriptor's exact endpoint and credential profile binding. It never resolves or reads a
/// credential: the selected runtime lease travels to <c>ProviderEgress</c>, which obtains the credential-read grant and
/// lease. An unbound descriptor selects nothing and sends no credential.
/// </remarks>
public static class ProviderProfileAttemptBinding
{
    /// <summary>Selects a runtime lease for <paramref name="binding"/> when the descriptor carries one.</summary>
    /// <param name="binding">The descriptor binding, or null for an unbound operation.</param>
    /// <param name="operation">The protected operation for profile selection.</param>
    /// <param name="profileSelector">The engine-wide profile runtime selector.</param>
    /// <param name="providerId">The provider identity used in typed failures.</param>
    /// <param name="cancellationToken">A token used to cancel selection.</param>
    /// <returns>A successful selection (possibly without a runtime) or a typed failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profileSelector"/> is null.</exception>
    public static async ValueTask<ProfileRuntimeSelection> SelectRuntimeAsync(
        ProviderOperationBinding? binding,
        ProtectedSemanticOperationContext? operation,
        IProviderProfileRuntimeSelector profileSelector,
        ProviderId providerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profileSelector);

        if (binding is null)
        {
            return ProfileRuntimeSelection.Unbound;
        }

        if (operation is null)
        {
            return ProfileRuntimeSelection.FromFailure(new ProviderFailure(
                ProviderFailureKind.Authorization,
                providerId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                "No protected operation context was supplied, so the credential profile cannot be selected.",
                diagnosticCause: null,
                ExtensionData.Empty));
        }

        var selection = await profileSelector
            .SelectAsync(binding, operation, cancellationToken)
            .ConfigureAwait(false);

        return selection is ProviderProfileRuntimeUnavailable unavailable
            ? ProfileRuntimeSelection.FromFailure(unavailable.Failure)
            : ProfileRuntimeSelection.FromRuntime(((ProviderProfileRuntimeSelected) selection).Runtime);
    }

    /// <summary>Normalizes a base address for relative URI resolution.</summary>
    /// <param name="baseAddress">The endpoint base address.</param>
    /// <returns>The same address with a trailing slash.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseAddress"/> is null.</exception>
    public static Uri NormalizeBaseAddress(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        return baseAddress.AbsoluteUri.EndsWith('/')
            ? baseAddress
            : new Uri(baseAddress.AbsoluteUri + "/");
    }

    /// <summary>The outcome of selecting the profile runtime for one attempt.</summary>
    public readonly struct ProfileRuntimeSelection
    {
        private ProfileRuntimeSelection(IProviderProfileRuntimeLease? runtime, ProviderFailure? failure)
        {
            Runtime = runtime;
            Failure = failure;
        }

        /// <summary>Gets the selection for an unbound operation: no runtime, no failure.</summary>
        public static ProfileRuntimeSelection Unbound => default;

        /// <summary>Gets the selected runtime lease the caller must dispose after the attempt, or null when unbound.</summary>
        public IProviderProfileRuntimeLease? Runtime { get; }

        /// <summary>Gets the endpoint base address the profile binding supplies, or null when unbound.</summary>
        public Uri? EndpointBaseAddress => Runtime?.Endpoint.BaseAddress;

        /// <summary>Gets the typed failure when selection did not succeed.</summary>
        public ProviderFailure? Failure { get; }

        /// <summary>Gets whether selection succeeded.</summary>
        public bool IsSuccess => Failure is null;

        /// <summary>Creates a successful selection.</summary>
        /// <param name="runtime">The selected runtime lease.</param>
        /// <returns>A successful selection value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="runtime"/> is null.</exception>
        public static ProfileRuntimeSelection FromRuntime(IProviderProfileRuntimeLease runtime)
        {
            ArgumentNullException.ThrowIfNull(runtime);
            return new(runtime, failure: null);
        }

        /// <summary>Creates a failed selection.</summary>
        /// <param name="failure">The typed failure.</param>
        /// <returns>A failed selection value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
        public static ProfileRuntimeSelection FromFailure(ProviderFailure failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new(runtime: null, failure);
        }
    }
}

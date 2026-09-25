// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using System.Diagnostics;

/// <summary>Shared profile-bound send preparation for native conversational adapters.</summary>
public static class NativeProviderChatSend
{
    /// <summary>Resolved credentials and endpoint override for one chat attempt.</summary>
    public sealed class AttemptContext: IAsyncDisposable
    {
        internal AttemptContext(
            Activity? activity,
            TimeProvider timeProvider,
            long sendStartedTimestamp,
            IProviderProfileRuntimeLease? lease,
            IProviderCredentialSource? credentialSource,
            Uri? endpointBaseOverride,
            ProviderFailure? failure)
        {
            Activity = activity;
            _timeProvider = timeProvider;
            _sendStartedTimestamp = sendStartedTimestamp;
            Lease = lease;
            CredentialSource = credentialSource;
            EndpointBaseOverride = endpointBaseOverride;
            Failure = failure;
        }

        private readonly TimeProvider _timeProvider;
        private readonly long _sendStartedTimestamp;

        /// <summary>Gets the observability activity for this attempt, when enabled.</summary>
        public Activity? Activity { get; }

        /// <summary>Gets the runtime lease that must be disposed after the attempt.</summary>
        public IProviderProfileRuntimeLease? Lease { get; }

        /// <summary>Gets the credential source to use when resolution succeeded.</summary>
        public IProviderCredentialSource? CredentialSource { get; }

        /// <summary>Gets the endpoint base address override from profile binding, when present.</summary>
        public Uri? EndpointBaseOverride { get; }

        /// <summary>Gets the typed failure when profile or credential resolution failed.</summary>
        public ProviderFailure? Failure { get; }

        /// <summary>Gets whether send preparation succeeded.</summary>
        public bool IsSuccess => Failure is null && CredentialSource is not null;

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Activity?.Dispose();
            return Lease is null ? ValueTask.CompletedTask : Lease.DisposeAsync();
        }

        /// <summary>Records a failed terminal outcome on observability instruments.</summary>
        public void RecordFailed()
        {
            ProviderRequestObservability.RecordRequest(
                "chat",
                "failed",
                _timeProvider.GetElapsedTime(_sendStartedTimestamp));
        }
    }

    /// <summary>
    /// Starts observability, resolves profile-bound credentials, and returns an attempt context that must be disposed.
    /// </summary>
    /// <param name="descriptor">The model descriptor for the attempt.</param>
    /// <param name="request">The conversational request.</param>
    /// <param name="fallbackCredentials">The legacy credential source.</param>
    /// <param name="profileSelector">The optional profile runtime selector.</param>
    /// <param name="timeProvider">The clock used for timing.</param>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>The attempt context.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static async ValueTask<AttemptContext> BeginAsync(
        ModelDescriptor descriptor,
        LlmModelRequest request,
        IProviderCredentialSource fallbackCredentials,
        IProviderProfileRuntimeSelector? profileSelector,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(fallbackCredentials);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var sendStarted = timeProvider.GetTimestamp();
        var activity = ProviderRequestObservability.StartChatSend(descriptor.ProviderId);

        var binding = await ProviderProfileAttemptBinding.ResolveCredentialSourceAsync(
                descriptor.Binding,
                request.Operation,
                fallbackCredentials,
                profileSelector,
                descriptor.ProviderId,
                cancellationToken)
            .ConfigureAwait(false);

        if (!binding.IsSuccess)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, binding.Failure!.SafeMessage);
            var failed = new AttemptContext(
                activity,
                timeProvider,
                sendStarted,
                lease: null,
                credentialSource: null,
                endpointBaseOverride: null,
                binding.Failure);
            failed.RecordFailed();
            return failed;
        }

        return new AttemptContext(
            activity,
            timeProvider,
            sendStarted,
            binding.Lease,
            binding.CredentialSource,
            binding.EndpointBaseAddress,
            failure: null);
    }
}

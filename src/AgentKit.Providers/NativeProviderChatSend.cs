// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using System.Diagnostics;

using AgentKit.Providers.Egress;

/// <summary>Shared profile-bound send preparation for native conversational adapters.</summary>
public static class NativeProviderChatSend
{
    /// <summary>Selected profile runtime and endpoint override for one chat attempt.</summary>
    public sealed class AttemptContext: IAsyncDisposable
    {
        internal AttemptContext(
            Activity? activity,
            TimeProvider timeProvider,
            long sendStartedTimestamp,
            IProviderProfileRuntimeLease? lease,
            Uri? endpointBaseOverride,
            ProviderFailure? failure)
        {
            Activity = activity;
            _timeProvider = timeProvider;
            _sendStartedTimestamp = sendStartedTimestamp;
            Lease = lease;
            EndpointBaseOverride = endpointBaseOverride;
            Failure = failure;
        }

        private readonly TimeProvider _timeProvider;
        private readonly long _sendStartedTimestamp;

        /// <summary>Gets the observability activity for this attempt, when enabled.</summary>
        public Activity? Activity { get; }

        /// <summary>Gets the runtime lease that must be disposed after the attempt.</summary>
        public IProviderProfileRuntimeLease? Lease { get; }

        /// <summary>Gets the endpoint base address override from profile binding, when present.</summary>
        public Uri? EndpointBaseOverride { get; }

        /// <summary>Creates the egress credential selection for this attempt's runtime lease.</summary>
        /// <param name="scheme">The branded provider's verified API-key header shape.</param>
        /// <returns>The selection egress resolves a credential-read grant and lease from, or null for an unbound operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="scheme"/> is null.</exception>
        public ProviderEgressCredential? CreateCredential(ProviderAuthorizationScheme scheme)
        {
            ArgumentNullException.ThrowIfNull(scheme);
            return Lease is null ? null : new ProviderEgressCredential(Lease, scheme);
        }

        /// <summary>Gets the typed failure when profile selection failed.</summary>
        public ProviderFailure? Failure { get; }

        /// <summary>Gets whether send preparation succeeded.</summary>
        public bool IsSuccess => Failure is null;

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
    /// Starts observability, selects the profile runtime, and returns an attempt context that must be disposed.
    /// </summary>
    /// <param name="descriptor">The model descriptor for the attempt.</param>
    /// <param name="request">The conversational request.</param>
    /// <param name="profileSelector">The engine-wide profile runtime selector.</param>
    /// <param name="timeProvider">The clock used for timing.</param>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>The attempt context.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static async ValueTask<AttemptContext> BeginAsync(
        ModelDescriptor descriptor,
        LlmModelRequest request,
        IProviderProfileRuntimeSelector profileSelector,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(profileSelector);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var sendStarted = timeProvider.GetTimestamp();
        var activity = ProviderRequestObservability.StartChatSend(descriptor.ProviderId);

        ProviderProfileAttemptBinding.ProfileRuntimeSelection binding;
        try
        {
            binding = await ProviderProfileAttemptBinding.SelectRuntimeAsync(
                    descriptor.Binding,
                    request.Operation,
                    profileSelector,
                    descriptor.ProviderId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            binding = ProviderProfileAttemptBinding.ProfileRuntimeSelection.FromFailure(new ProviderFailure(
                exception is OperationCanceledException ? ProviderFailureKind.Timeout : ProviderFailureKind.Authentication,
                descriptor.ProviderId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                exception is OperationCanceledException
                    ? "The credential profile was not selected before its own deadline."
                    : "The credential profile could not be selected.",
                exception,
                ExtensionData.Empty));
        }
        catch (OperationCanceledException exception)
        {
            binding = ProviderProfileAttemptBinding.ProfileRuntimeSelection.FromFailure(new ProviderFailure(
                ProviderFailureKind.Cancellation,
                descriptor.ProviderId,
                requestId: null,
                statusCode: null,
                providerCode: null,
                retryAfter: null,
                "The attempt was cancelled.",
                exception,
                ExtensionData.Empty));
        }

        if (!binding.IsSuccess)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, binding.Failure!.SafeMessage);
            var failed = new AttemptContext(
                activity,
                timeProvider,
                sendStarted,
                lease: null,
                endpointBaseOverride: null,
                binding.Failure);
            failed.RecordFailed();
            return failed;
        }

        return new AttemptContext(
            activity,
            timeProvider,
            sendStarted,
            binding.Runtime,
            binding.EndpointBaseAddress,
            failure: null);
    }
}

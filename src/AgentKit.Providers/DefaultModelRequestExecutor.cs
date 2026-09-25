// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using Microsoft.Extensions.Options;

/// <summary>
/// The first-party model request executor. It resolves one selected descriptor to an
/// <see cref="ILlmModel"/>, performs bounded same-model retries while no response event has been observed, and
/// returns <see cref="ModelFallbackRequired"/> for retryable failures when semantic fallback is enabled.
/// </summary>
/// <remarks>
/// The executor never selects another model. Cross-model fallback is signaled to the loop through
/// <see cref="ModelFallbackRequired"/> only when runtime options and the captured fallback policy allow it.
/// </remarks>
internal sealed partial class DefaultModelRequestExecutor(
    ILlmModelResolver modelResolver,
    IOptions<AgentProviderRuntimeOptions> runtimeOptions,
    TimeProvider timeProvider,
    ILogger<DefaultModelRequestExecutor> logger): IModelRequestExecutor
{
    private readonly ILlmModelResolver _modelResolver =
        modelResolver ?? throw new ArgumentNullException(nameof(modelResolver));

    private readonly AgentProviderRuntimeOptions _runtimeOptions =
        runtimeOptions?.Value ?? throw new ArgumentNullException(nameof(runtimeOptions));

    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    private readonly ILogger<DefaultModelRequestExecutor> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task<ModelExecutionResult> ExecuteAsync(
        ModelExecutionRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(observer);

        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.ModelExecute);
        _ = activity?.SetTag(AgentKitTagNames.ModelRequestId, request.Context.ModelRequestId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.RequestModel, request.Selection.Model.Alias.ToString());
        _ = activity?.SetTag(AgentKitTagNames.ModelCatalogVersion, request.Selection.CatalogVersion.Value);

        var llmModel = _modelResolver.Resolve(request.Selection.Model);
        if (llmModel is null)
        {
            var missingAdapter = CreateMissingAdapterFailure(request.Selection.Model);
            activity.SetFailed("missing_adapter", nameof(ProviderFailureKind.InvalidRequest));
            ProviderMetrics.RecordExecution("missing_adapter");
            ProviderLog.ModelExecutionMissingAdapter(_logger, request.Selection.Model.Alias);
            return new ModelExecutionFailed(request.Selection, attempts: 1, missingAdapter);
        }

        var attempts = 0;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempts++;

                var trackingObserver = new EventTrackingModelResponseObserver(observer);
                var attemptRequest = CreateAttemptRequest(request, attempts);
                var preflightFailure = ModelRequestPreflight.Validate(attemptRequest, request.Selection.Model);
                if (preflightFailure is { } rejected)
                {
                    if (attempts >= request.RetryPolicy.MaximumAttempts
                        || !CanRetrySameModel(rejected, trackingObserver.HasObservedEvent))
                    {
                        return TerminalFailure(request, attempts, rejected, activity);
                    }

                    await DelayBeforeRetryAsync(request.RetryPolicy, attempts, rejected.RetryAfter, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                var attemptResult = await llmModel.ExecuteAsync(attemptRequest, trackingObserver, cancellationToken)
                    .ConfigureAwait(false);

                switch (attemptResult)
                {
                    case ModelAttemptCompleted completed:
                        activity.SetSuccessful("completed");
                        ProviderMetrics.RecordExecution("completed");
                        ProviderLog.ModelExecutionCompleted(
                            _logger,
                            request.Context.ModelRequestId,
                            request.Selection.Model.Alias,
                            attempts);
                        return new ModelExecutionCompleted(request.Selection, attempts, completed);

                    case ModelAttemptCancelled cancelled:
                        activity.SetFailed("cancelled", nameof(ProviderFailureKind.Cancellation));
                        ProviderMetrics.RecordExecution("cancelled");
                        ProviderLog.ModelExecutionCancelled(_logger, request.Context.ModelRequestId, attempts);
                        return new ModelExecutionCancelled(request.Selection, attempts, cancelled.Cancellation);

                    case ModelAttemptFailed failed:
                        if (attempts >= request.RetryPolicy.MaximumAttempts
                            || !CanRetrySameModel(failed.Failure, trackingObserver.HasObservedEvent))
                        {
                            return TerminalFailure(request, attempts, failed.Failure, activity);
                        }

                        await DelayBeforeRetryAsync(
                                request.RetryPolicy,
                                attempts,
                                failed.Failure.RetryAfter,
                                cancellationToken)
                            .ConfigureAwait(false);
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unexpected {nameof(ModelAttemptResult)} '{attemptResult.GetType().Name}'.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            ProviderMetrics.RecordExecution("cancelled");
            ProviderLog.ModelExecutionCancelled(_logger, request.Context.ModelRequestId, Math.Max(attempts, 1));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            ProviderMetrics.RecordExecution("failed");
            ProviderLog.ModelExecutionFailed(_logger, request.Context.ModelRequestId, errorType);
            throw;
        }
    }

    private ModelExecutionResult TerminalFailure(
        ModelExecutionRequest request,
        int attempts,
        ProviderFailure failure,
        Activity? activity)
    {
        if (ShouldRequireFallback(request, failure))
        {
            activity?.SetSuccessful("fallback_required");
            ProviderMetrics.RecordExecution("fallback_required");
            ProviderLog.ModelExecutionFallbackRequired(
                _logger,
                request.Context.ModelRequestId,
                request.Selection.Model.Alias,
                failure.Kind,
                attempts);
            return new ModelFallbackRequired(request.Selection, attempts, failure);
        }

        activity?.SetFailed(failure.Kind.ToString(), failure.Kind.ToString());
        ProviderMetrics.RecordExecution("failed");
        ProviderLog.ModelExecutionTerminalFailure(
            _logger,
            request.Context.ModelRequestId,
            request.Selection.Model.Alias,
            failure.Kind,
            attempts);
        return new ModelExecutionFailed(request.Selection, attempts, failure);
    }

    private bool ShouldRequireFallback(ModelExecutionRequest request, ProviderFailure failure) =>
        _runtimeOptions.AllowSemanticFallback
        && request.Fallback is ModelFallbackPolicy.OrderedCandidates
        && IsFallbackEligibleFailure(failure.Kind);

    private static bool CanRetrySameModel(ProviderFailure failure, bool observerEventObserved) =>
        !observerEventObserved && IsSameModelRetryableFailure(failure.Kind);

    private static bool IsSameModelRetryableFailure(ProviderFailureKind kind) =>
        kind is ProviderFailureKind.Throttling
            or ProviderFailureKind.Unavailable
            or ProviderFailureKind.Timeout;

    private static bool IsFallbackEligibleFailure(ProviderFailureKind kind) =>
        kind is ProviderFailureKind.Throttling
            or ProviderFailureKind.Unavailable
            or ProviderFailureKind.Timeout;

    private LlmModelRequest CreateAttemptRequest(ModelExecutionRequest request, int attempt)
    {
        var deadline = _timeProvider.GetUtcNow() + _runtimeOptions.RequestTimeout;
        var context = request.Context.Model.Equals(request.Selection.Model)
            ? request.Context
            : request.Context with { Model = request.Selection.Model };
        return new LlmModelRequest(context, attempt, deadline, ProviderRequestOptions.Empty, request.Operation);
    }

    private async Task DelayBeforeRetryAsync(
        ProviderRetryPolicy policy,
        int completedAttempts,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken)
    {
        var delay = ComputeRetryDelay(policy, completedAttempts, retryAfter);
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
    }

    private static TimeSpan ComputeRetryDelay(
        ProviderRetryPolicy policy,
        int completedAttempts,
        TimeSpan? retryAfter)
    {
        var backoff = policy.InitialDelay;
        if (completedAttempts > 1 && policy.InitialDelay > TimeSpan.Zero)
        {
            var exponent = Math.Min(completedAttempts - 1, 10);
            var scaledTicks = Math.Min(
                policy.InitialDelay.Ticks * (1L << exponent),
                policy.MaximumDelay.Ticks);
            backoff = TimeSpan.FromTicks(scaledTicks);
        }

        var delay = backoff;
        if (retryAfter is { } hint)
        {
            delay = hint <= policy.MaximumDelay ? hint : policy.MaximumDelay;
            if (delay < backoff)
            {
                delay = backoff;
            }
        }

        return delay <= policy.MaximumDelay ? delay : policy.MaximumDelay;
    }

    private static ProviderFailure CreateMissingAdapterFailure(ModelDescriptor model) =>
        new(
            ProviderFailureKind.InvalidRequest,
            model.ProviderId,
            null,
            null,
            null,
            null,
            $"No {nameof(ILlmModel)} adapter is registered for alias '{model.Alias}'.",
            null,
            ExtensionData.Empty);

    private sealed class EventTrackingModelResponseObserver(IModelResponseObserver inner): IModelResponseObserver
    {
        private readonly IModelResponseObserver _inner =
            inner ?? throw new ArgumentNullException(nameof(inner));

        public bool HasObservedEvent { get; private set; }

        public async ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default)
        {
            HasObservedEvent = true;
            await _inner.OnEventAsync(responseEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}

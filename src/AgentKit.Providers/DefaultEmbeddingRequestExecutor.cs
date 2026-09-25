// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using Microsoft.Extensions.Options;

/// <summary>The first-party embedding request executor.</summary>
internal sealed class DefaultEmbeddingRequestExecutor(
    IEmbeddingModelResolver embeddingModelResolver,
    IOptions<AgentProviderRuntimeOptions> runtimeOptions,
    TimeProvider timeProvider): IEmbeddingRequestExecutor
{
    private readonly IEmbeddingModelResolver _embeddingModelResolver =
        embeddingModelResolver ?? throw new ArgumentNullException(nameof(embeddingModelResolver));

    private readonly AgentProviderRuntimeOptions _runtimeOptions =
        runtimeOptions?.Value ?? throw new ArgumentNullException(nameof(runtimeOptions));

    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public async Task<EmbeddingExecutionResult> ExecuteAsync(
        EmbeddingExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var embeddingModel = _embeddingModelResolver.Resolve(request.Selection.Model);
        if (embeddingModel is null)
        {
            return new EmbeddingExecutionFailed(
                request.Selection,
                Attempts: 1,
                CreateMissingAdapterFailure(request.Selection.Model));
        }

        var requestId = new EmbeddingRequestId(Guid.NewGuid());
        var attempts = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            var attemptRequest = CreateAttemptRequest(request, requestId, attempts);
            var result = await embeddingModel.GenerateAsync(attemptRequest, cancellationToken).ConfigureAwait(false);

            switch (result)
            {
                case EmbeddingAttemptCompleted completed:
                    return new EmbeddingExecutionCompleted(request.Selection, attempts, completed.Response);
                case EmbeddingAttemptFailed failed:
                    if (attempts >= request.RetryPolicy.MaximumAttempts
                        || !IsSameModelRetryableFailure(failed.Failure.Kind))
                    {
                        return TerminalFailure(request, attempts, failed.Failure);
                    }

                    await DelayBeforeRetryAsync(request.RetryPolicy, attempts, failed.Failure.RetryAfter, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case EmbeddingAttemptCancelled cancelled:
                    return new EmbeddingExecutionFailed(request.Selection, attempts, cancelled.Cancellation);
                default:
                    throw new InvalidOperationException($"Unexpected {nameof(EmbeddingAttemptResult)} '{result.GetType().Name}'.");
            }
        }
    }

    private EmbeddingExecutionResult TerminalFailure(
        EmbeddingExecutionRequest request,
        int attempts,
        ProviderFailure failure)
    {
        return _runtimeOptions.AllowSemanticFallback
               && request.Fallback is SemanticFallbackPolicy.OrderedCandidates
               && IsFallbackEligibleFailure(failure.Kind)
            ? new EmbeddingFallbackRequired(request.Selection, attempts, failure)
            : new EmbeddingExecutionFailed(request.Selection, attempts, failure);
    }

    private static bool IsSameModelRetryableFailure(ProviderFailureKind kind) =>
        kind is ProviderFailureKind.Throttling or ProviderFailureKind.Unavailable or ProviderFailureKind.Timeout;

    private static bool IsFallbackEligibleFailure(ProviderFailureKind kind) => IsSameModelRetryableFailure(kind);

    private EmbeddingModelRequest CreateAttemptRequest(
        EmbeddingExecutionRequest request,
        EmbeddingRequestId requestId,
        int attempt)
    {
        var deadline = _timeProvider.GetUtcNow() + _runtimeOptions.RequestTimeout;
        var context = new EmbeddingRequestContext(
            requestId,
            request.Selection.Model,
            request.Request);
        return new EmbeddingModelRequest(context, attempt, deadline, ProviderRequestOptions.Empty)
        {
            Operation = request.Operation,
        };
    }

    private async Task DelayBeforeRetryAsync(
        SemanticOperationRetryPolicy policy,
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
        SemanticOperationRetryPolicy policy,
        int completedAttempts,
        TimeSpan? retryAfter)
    {
        var backoff = policy.InitialDelay;
        if (completedAttempts > 1 && policy.InitialDelay > TimeSpan.Zero)
        {
            var exponent = Math.Min(completedAttempts - 1, 10);
            var scaledTicks = Math.Min(policy.InitialDelay.Ticks * (1L << exponent), policy.MaximumDelay.Ticks);
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

    private static ProviderFailure CreateMissingAdapterFailure(EmbeddingModelDescriptor model) =>
        new(
            ProviderFailureKind.InvalidRequest,
            model.ProviderId,
            null,
            null,
            null,
            null,
            $"No {nameof(IEmbeddingModel)} adapter is registered for alias '{model.Alias}'.",
            null,
            ExtensionData.Empty);
}

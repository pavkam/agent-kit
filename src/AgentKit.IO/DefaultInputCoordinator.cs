// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Coordinates bounded validation, admission evidence, and atomic promotion through one selected input queue.</summary>
/// <remarks>
/// The coordinator owns no session truth and performs no durable mutation itself. It bounds the payload, allocates the admission identity,
/// captures the canonical preprocessing manifest and the injected-clock timestamp, and then reports exactly what the selected queue committed.
/// It applies no preprocessors and decides no authorization: the request's captured authorization evidence is carried to the queue's store,
/// which enforces it. A returned success therefore means the queue committed, never that the coordinator assumed it would.
/// </remarks>
internal sealed class DefaultInputCoordinator: IInputCoordinator
{
    private readonly IInputQueue _queue;
    private readonly IIdentifierGenerator<AdmissionId> _admissionIds;
    private readonly TimeProvider _timeProvider;
    private readonly InputCoordinatorOptions _options;
    private readonly ILogger<DefaultInputCoordinator> _logger;

    /// <summary>Initializes coordination over an explicitly selected queue and identity source.</summary>
    /// <param name="queue">The non-null selected durable input queue.</param>
    /// <param name="admissionIds">The non-null allocator for new admission identities.</param>
    /// <param name="timeProvider">The non-null injected clock used for admission timestamps and elapsed measurement.</param>
    /// <param name="options">The non-null validated coordinator bounds and preprocessing evidence.</param>
    /// <param name="logger">The optional content-free structured logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultInputCoordinator(
        IInputQueue queue,
        IIdentifierGenerator<AdmissionId> admissionIds,
        TimeProvider timeProvider,
        InputCoordinatorOptions options,
        ILogger<DefaultInputCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(admissionIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _queue = queue;
        _admissionIds = admissionIds;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultInputCoordinator>.Instance;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The selected identity generator produced a default admission identity.</exception>
    public async ValueTask<InputAdmissionResult> AdmitAsync(
        InputAdmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.InputAdmission,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, request.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.SessionId.ToString() },
                { AgentKitTagNames.ExecutionLaneId, request.ExecutionLaneId.ToString() },
                { AgentKitTagNames.OperationId, request.Correlation.OperationId.ToString() },
            });
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Input.Parts.Length > _options.MaximumInputParts)
            {
                var rejected = new RejectedInput(new InputRejection(
                    InputRejectionKind.InvalidInput,
                    "The input carries more content parts than this coordinator admits."));
                FinishAdmission(activity, InputAdmissionOutcome.Rejected, started, null);
                return rejected;
            }

            var admissionId = _admissionIds.Create();
            if (admissionId == default)
            {
                throw new InvalidOperationException(
                    "The selected admission identity generator produced a default identity.");
            }

            // The first-party coordinator applies no preprocessors, so the effective payload is the
            // original payload and both manifest fingerprints are the same canonical digest.
            var fingerprint = InputPayloadFingerprint.Create(request.Input);
            var preprocessing = new InputPreprocessingManifest(
                _options.PreprocessingConfigurationVersion,
                fingerprint,
                fingerprint);

            var result = await _queue.AppendAsync(
                request,
                admissionId,
                request.Input,
                preprocessing,
                _timeProvider.GetUtcNow(),
                cancellationToken).ConfigureAwait(false);
            FinishAdmission(activity, Classify(result), started, null);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishAdmission(activity, InputAdmissionOutcome.Cancelled, started, nameof(OperationCanceledException));
            throw;
        }
        catch (Exception exception)
        {
            FinishAdmission(activity, InputAdmissionOutcome.Failed, started, ErrorType(exception));
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<InputPromotionResult> PromoteAsync(
        InputPromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = TryGetTimestamp();
        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.InputPromotion,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.AgentId, request.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.SessionId.ToString() },
                { AgentKitTagNames.ExecutionLaneId, request.ExecutionLaneId.ToString() },
                { AgentKitTagNames.RunId, request.ExpectedOperation.RunId.ToString() },
                { AgentKitTagNames.OperationId, request.ExpectedOperation.OperationId.ToString() },
                { AgentKitTagNames.InputPromotionBoundary, request.Boundary.ToString() },
            });
        var activity = activityScope.Activity;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _queue.PromoteAsync(request, cancellationToken).ConfigureAwait(false);
            FinishPromotion(activity, request.Boundary, Classify(result), started, null);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FinishPromotion(activity, request.Boundary, InputPromotionOutcome.Cancelled, started, nameof(OperationCanceledException));
            throw;
        }
        catch (Exception exception)
        {
            FinishPromotion(activity, request.Boundary, InputPromotionOutcome.Failed, started, ErrorType(exception));
            throw;
        }
    }

    /// <summary>Classifies the queue's committed admission outcome for diagnostics only.</summary>
    /// <param name="result">The non-null terminal admission result.</param>
    /// <returns>The bounded diagnostic outcome matching the result's closed family.</returns>
    private static InputAdmissionOutcome Classify(InputAdmissionResult result)
    {
        Debug.Assert(result is not null, "The selected queue returns a terminal admission result.");
        return result switch
        {
            AcceptedInput => InputAdmissionOutcome.Accepted,
            InputConflict => InputAdmissionOutcome.Conflict,
            QueueCapacityExceeded => InputAdmissionOutcome.CapacityExceeded,
            RejectedInput => InputAdmissionOutcome.Rejected,
            _ => InputAdmissionOutcome.Failed,
        };
    }

    /// <summary>Classifies the queue's committed promotion outcome for diagnostics only.</summary>
    /// <param name="result">The non-null terminal promotion result.</param>
    /// <returns>The bounded diagnostic outcome matching the result's closed family.</returns>
    private static InputPromotionOutcome Classify(InputPromotionResult result)
    {
        Debug.Assert(result is not null, "The selected queue returns a terminal promotion result.");
        return result switch
        {
            InputPromoted => InputPromotionOutcome.Promoted,
            InputPromotionConflict => InputPromotionOutcome.Conflict,
            InputPromotionRejected => InputPromotionOutcome.Rejected,
            _ => InputPromotionOutcome.Failed,
        };
    }

    /// <summary>Returns the stable exception type name used in content-free diagnostics.</summary>
    /// <param name="exception">The non-null observed exception.</param>
    /// <returns>The exception's full type name, or its short name when no full name exists.</returns>
    private static string ErrorType(Exception exception)
    {
        Debug.Assert(exception is not null, "Only observed exceptions are classified.");
        return exception.GetType().FullName ?? exception.GetType().Name;
    }

    /// <summary>Records the terminal admission outcome without changing it.</summary>
    /// <param name="activity">The owning operation activity.</param><param name="outcome">The bounded terminal outcome.</param>
    /// <param name="started">The optional start timestamp.</param><param name="errorType">The optional stable error type.</param>
    private void FinishAdmission(Activity? activity, InputAdmissionOutcome outcome, long? started, string? errorType)
    {
        var outcomeValue = outcome.ToStableValue();
        SafeSetActivity(() =>
        {
            if (outcome == InputAdmissionOutcome.Accepted)
            {
                activity.SetSuccessful(outcomeValue);
            }
            else
            {
                activity.SetFailed(outcomeValue, errorType ?? outcomeValue);
            }
        });
        try
        {
            if (errorType is not null && outcome == InputAdmissionOutcome.Failed)
            {
                IOLog.InputAdmissionFailed(_logger, errorType);
            }
            else
            {
                IOLog.InputAdmissionCompleted(_logger, outcomeValue);
            }
        }
        catch
        {
            // Logging is observational and cannot alter the queue's committed admission outcome.
        }

        var elapsed = TryGetElapsedTime(started);
        try
        {
            IOMetrics.RecordInputAdmission(outcome, elapsed);
        }
        catch
        {
            // Metrics are observational and cannot alter the queue's committed admission outcome.
        }
    }

    /// <summary>Records the terminal promotion outcome without changing it.</summary>
    /// <param name="activity">The owning operation activity.</param><param name="boundary">The requested safe boundary.</param>
    /// <param name="outcome">The bounded terminal outcome.</param><param name="started">The optional start timestamp.</param>
    /// <param name="errorType">The optional stable error type.</param>
    private void FinishPromotion(Activity? activity, PromotionBoundary boundary, InputPromotionOutcome outcome, long? started, string? errorType)
    {
        var outcomeValue = outcome.ToStableValue();
        SafeSetActivity(() =>
        {
            if (outcome == InputPromotionOutcome.Promoted)
            {
                activity.SetSuccessful(outcomeValue);
            }
            else
            {
                activity.SetFailed(outcomeValue, errorType ?? outcomeValue);
            }
        });
        try
        {
            if (errorType is not null && outcome == InputPromotionOutcome.Failed)
            {
                IOLog.InputPromotionFailed(_logger, boundary, errorType);
            }
            else
            {
                IOLog.InputPromotionCompleted(_logger, boundary, outcomeValue);
            }
        }
        catch
        {
            // Logging is observational and cannot alter the queue's committed promotion outcome.
        }

        var elapsed = TryGetElapsedTime(started);
        try
        {
            IOMetrics.RecordInputPromotion(boundary, outcome, elapsed);
        }
        catch
        {
            // Metrics are observational and cannot alter the queue's committed promotion outcome.
        }
    }

    /// <summary>Reads a start timestamp without letting a failing clock change coordination.</summary>
    /// <returns>The timestamp, or null when the injected clock failed.</returns>
    private long? TryGetTimestamp()
    {
        try
        {
            return _timeProvider.GetTimestamp();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Measures elapsed time without letting a failing clock change coordination.</summary>
    /// <param name="started">The optional start timestamp.</param>
    /// <returns>The measured duration, or null when it is unavailable.</returns>
    private TimeSpan? TryGetElapsedTime(long? started)
    {
        if (started is not { } timestamp)
        {
            return null;
        }

        try
        {
            return _timeProvider.GetElapsedTime(timestamp);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Applies an activity update without letting a listener change coordination.</summary>
    /// <param name="action">The non-null activity update.</param>
    private static void SafeSetActivity(Action action)
    {
        Debug.Assert(action is not null, "Only package-owned activity updates are applied.");
        try
        {
            action();
        }
        catch
        {
            // Activity listeners cannot alter a committed admission or promotion outcome.
        }
    }
}

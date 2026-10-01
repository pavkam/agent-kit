// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Externalizes oversized tool results through one keyed artifact coordinator.</summary>
/// <remarks>
/// <para>
/// The spill is bound to the coordinator it is constructed over; it never selects a coordinator or profile. It prepares the complete
/// content under the call's captured authorization, finalizes it, and aborts the staging when finalization fails, so a refusal leaves
/// no readable artifact. The artifact is owned by the call's session (or run) and retained under the configured policy; because the
/// reference is committed by the run's tool-result message rather than by this spill, no reconciliation intent is recorded and
/// reconciliation never collects the artifact. It never appends a session record.
/// </para>
/// <para>Instances are immutable and safe for concurrent use. Every refusal is a typed outcome and every signal is content-free.</para>
/// </remarks>
public sealed class ArtifactToolResultSpill: IToolResultSpill
{
    private readonly IArtifactCoordinator _artifacts;
    private readonly ArtifactDirectoryId _directory;
    private readonly ArtifactRetentionPolicyKey _retentionPolicy;
    private readonly DataClassification _classification;
    private readonly string _mediaType;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _time;
    private readonly ILogger<ArtifactToolResultSpill> _logger;

    /// <summary>Initializes a spill over one coordinator and validated options.</summary>
    /// <param name="artifacts">The protected artifact coordinator this spill stores through.</param>
    /// <param name="options">The directory, retention, classification, media type, and timeout; validated and copied now.</param>
    /// <param name="time">The clock bounding the spill sequence.</param>
    /// <param name="logger">The type-specific structured logger.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">The directory, retention policy, or media type is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The classification is undefined or the timeout is not positive.</exception>
    public ArtifactToolResultSpill(
        IArtifactCoordinator artifacts,
        ToolResultSpillOptions options,
        TimeProvider time,
        ILogger<ArtifactToolResultSpill> logger)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Directory.Value, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RetentionPolicy.Value, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.MediaType, nameof(options));
        ArgumentOutOfRangeException.ThrowIfUndefined(options.Classification, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.Timeout, TimeSpan.Zero, nameof(options));
        _artifacts = artifacts;
        _directory = options.Directory;
        _retentionPolicy = options.RetentionPolicy;
        _classification = options.Classification;
        _mediaType = options.MediaType;
        _timeout = options.Timeout;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<ToolResultSpillResult> SpillAsync(ToolResultSpillRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var call = request.Call;
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.ToolResultSpill,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.ToolResultSpill },
                { AgentKitTagNames.ToolId, call.Tool.Id.ToString() },
                { AgentKitTagNames.ToolCallId, call.CallId.ToString() },
            });
        using var deadline = new CancellationTokenSource(_timeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            var result = await StoreAsync(request, linked.Token).ConfigureAwait(false);
            Observe(scope, call, result is ToolResultSpilled ? "spilled" : "refused", result is ToolResultSpilled);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Observe(scope, call, "timed_out", succeeded: false);
            return new ToolResultNotSpilled("The result could not be stored before its time limit.");
        }
        catch (OperationCanceledException)
        {
            Observe(scope, call, "cancelled", succeeded: false);
            throw;
        }
        catch (Exception exception)
        {
            ToolLog.Failed(_logger, call.CallId, call.Tool.Id, exception.GetType().Name);
            Observe(scope, call, "faulted", succeeded: false);
            return new ToolResultNotSpilled("The artifact coordinator could not store the result.");
        }
    }

    private async Task<ToolResultSpillResult> StoreAsync(ToolResultSpillRequest request, CancellationToken cancellationToken)
    {
        var call = request.Call;
        var authorization = call.Authorization;
        var scope = authorization.Scope;
        var key = $"tool-result-spill:{call.CallId}";
        var content = request.Content;
        var metadata = new ArtifactMetadata(
            Owner(scope),
            _mediaType,
            content.Length,
            FileSecurityBinding.ContentFingerprint(content.AsSpan()),
            _classification,
            scope.SessionId.HasValue ? ArtifactOwnershipKind.Session : ArtifactOwnershipKind.Run,
            ArtifactMutability.Immutable,
            new ArtifactRetention(_retentionPolicy, null, false),
            null);
        using var stream = new MemoryStream([.. content], writable: false);
        var prepared = await _artifacts.PrepareAsync(
            new ArtifactPrepareRequest(
                scope.AgentId, scope.SessionId, call.CallId, scope.Correlation, authorization, _directory, metadata, stream,
                new IdempotencyKey($"{key}:prepare")),
            cancellationToken).ConfigureAwait(false);
        if (prepared is not ArtifactPrepared staged)
        {
            return new ToolResultNotSpilled("The result could not be staged as an artifact.");
        }

        var finalized = await _artifacts.FinalizeAsync(
            new ArtifactFinalizeRequest(
                staged.PreparationId, scope.AgentId, scope.SessionId, call.CallId, scope.Correlation, authorization,
                new IdempotencyKey($"{key}:finalize")),
            cancellationToken).ConfigureAwait(false);
        if (finalized is ArtifactFinalized committed)
        {
            return new ToolResultSpilled(committed.Reference);
        }

        _ = await _artifacts.AbortAsync(
            new ArtifactAbortRequest(
                staged.PreparationId, scope.AgentId, scope.SessionId, scope.Correlation, authorization,
                ArtifactAbortReason.ReferenceCommitFailure, new IdempotencyKey($"{key}:abort")),
            CancellationToken.None).ConfigureAwait(false);
        return new ToolResultNotSpilled("The result could not be published as an artifact.");
    }

    private void Observe(AgentKitActivityScope scope, ValidatedToolCall call, string outcome, bool succeeded)
    {
        try
        {
            if (succeeded)
            {
                scope.Activity.SetSuccessful(outcome);
            }
            else
            {
                scope.Activity.SetFailed(outcome, outcome);
            }
        }
        catch
        {
            // Instrumentation is observational only and cannot change the spill outcome.
        }

        try
        {
            ToolLog.ResultSpillCompleted(_logger, succeeded ? LogLevel.Information : LogLevel.Warning, call.CallId, call.Tool.Id, outcome);
            ToolRecordingMetrics.ResultSpillCount.Add(1, new TagList { { AgentKitTagNames.Outcome, outcome } });
        }
        catch
        {
            // Instrumentation is observational only and cannot change the spill outcome.
        }
    }

    private static ArtifactOwnerId Owner(SecurityAuthorizationScope scope) => scope.SessionId is { } sessionId
        ? new ArtifactOwnerId($"session:{sessionId}")
        : scope.Correlation is InRunOperationCorrelation inRun
            ? new ArtifactOwnerId($"run:{inRun.RunId}")
            : new ArtifactOwnerId($"operation:{scope.Correlation.OperationId}");
}

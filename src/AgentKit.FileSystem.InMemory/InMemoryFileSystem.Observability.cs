// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

public sealed partial class InMemoryFileSystem
{
    /// <inheritdoc/>
    public Task<FileReadResult> ReadAsync(FileReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveTaskAsync(
            "read", request.Grant.RequestId, token => ReadCoreAsync(request, token),
            static result => result is FileRead ? "read" : result.GetType().Name, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<FileWriteResult> WriteAsync(FileWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveTaskAsync(
            "write", request.Grant.RequestId, token => WriteCoreAsync(request, token),
            static result => result is FileWritten ? "written" : result.GetType().Name, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DirectoryEnumerationResult> EnumerateAsync(
        DirectoryEnumerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveValueTaskAsync(
            "enumerate", request.Grant.RequestId, token => EnumerateCoreAsync(request, token),
            static result => result.Status.ToString(), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<GlobResult> GlobAsync(GlobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveValueTaskAsync(
            "glob", request.Grant.RequestId, token => GlobCoreAsync(request, token),
            static result => result.Status.ToString(), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<FileSearchResult> SearchAsync(FileSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveValueTaskAsync(
            "search", request.Grant.RequestId, token => SearchCoreAsync(request, token),
            static result => result.Status.ToString(), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<FileSnapshotResult> ReadSnapshotAsync(
        FileSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveValueTaskAsync(
            "snapshot", request.Grant.RequestId, token => ReadSnapshotCoreAsync(request, token),
            static result => result.Status.ToString(), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<AtomicFileReplaceResult> ReplaceAsync(
        AtomicFileReplaceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveValueTaskAsync(
            "replace", request.Grant.RequestId, token => ReplaceCoreAsync(request, token),
            static result => result.Status.ToString(), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<WorkspacePatchResult> ApplyPatchAsync(
        WorkspacePatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ObserveValueTaskAsync(
            "patch", requestId: null, token => ApplyPatchCoreAsync(request, token),
            static result => result.Status.ToString(), cancellationToken);
    }

    private async Task<TResult> ObserveTaskAsync<TResult>(
        string operation,
        SecurityRequestId? requestId,
        Func<CancellationToken, Task<TResult>> action,
        Func<TResult, string> classify,
        CancellationToken cancellationToken)
    {
        using var activity = StartFileSystemActivity(operation, requestId);
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            CompleteFileSystemObservation(activity, operation, requestId, classify(result));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FailFileSystemObservation(activity, operation, requestId, "cancelled", nameof(OperationCanceledException));
            throw;
        }
        catch (Exception exception)
        {
            FailFileSystemObservation(
                activity, operation, requestId, "failed", exception.GetType().FullName ?? exception.GetType().Name);
            InMemoryFileSystemLog.Failed(
                _logger, operation, requestId, exception.GetType().FullName ?? exception.GetType().Name);
            throw;
        }
    }

    private async ValueTask<TResult> ObserveValueTaskAsync<TResult>(
        string operation,
        SecurityRequestId? requestId,
        Func<CancellationToken, ValueTask<TResult>> action,
        Func<TResult, string> classify,
        CancellationToken cancellationToken)
    {
        using var activity = StartFileSystemActivity(operation, requestId);
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(false);
            CompleteFileSystemObservation(activity, operation, requestId, classify(result));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FailFileSystemObservation(activity, operation, requestId, "cancelled", nameof(OperationCanceledException));
            throw;
        }
        catch (Exception exception)
        {
            FailFileSystemObservation(
                activity, operation, requestId, "failed", exception.GetType().FullName ?? exception.GetType().Name);
            InMemoryFileSystemLog.Failed(
                _logger, operation, requestId, exception.GetType().FullName ?? exception.GetType().Name);
            throw;
        }
    }

    private static Activity? StartFileSystemActivity(string operation, SecurityRequestId? requestId)
    {
        var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.FileSystemOperation);
        _ = activity?.SetTag(AgentKitTagNames.FileSystemOperation, operation);
        _ = activity?.SetTag(AgentKitTagNames.SecurityRequestId, requestId?.ToString());
        return activity;
    }

    private void CompleteFileSystemObservation(Activity? activity, string operation, SecurityRequestId? requestId, string outcome)
    {
        var normalized = NormalizeOutcome(outcome);
        if (IsFailureOutcome(normalized))
        {
            activity.SetFailed(normalized, outcome);
        }
        else
        {
            activity.SetSuccessful(normalized);
        }

        InMemoryFileSystemLog.Completed(_logger, operation, requestId, outcome);
        InMemoryFileSystemMetrics.Record(operation, normalized);
    }

    private void FailFileSystemObservation(
        Activity? activity, string operation, SecurityRequestId? requestId, string outcome, string errorType)
    {
        activity.SetFailed(outcome, errorType);
        InMemoryFileSystemLog.Completed(_logger, operation, requestId, outcome);
        InMemoryFileSystemMetrics.Record(operation, outcome);
    }

    private static string NormalizeOutcome(string outcome) => outcome.ToLowerInvariant();

    private static bool IsFailureOutcome(string outcome) =>
        outcome.Contains("fail", StringComparison.Ordinal)
        || outcome.Contains("partial", StringComparison.Ordinal)
        || outcome.Contains("unknown", StringComparison.Ordinal);
}

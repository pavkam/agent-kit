// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

public sealed partial class InMemoryFileSystem
{
    /// <inheritdoc/>
    private async ValueTask<FileSnapshotResult> ReadSnapshotCoreAsync(
        FileSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumBytes);
        ArgumentNullException.ThrowIfNull(request.Grant);
        cancellationToken.ThrowIfCancellationRequested();

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(request.Path)],
            FileSecurityBinding.SnapshotFingerprint(request.Path, request.MaximumBytes));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(request.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, intent))
        {
            return SnapshotFailure(FileSnapshotStatus.Denied, FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        if (request.MaximumBytes > _maximumReadBytes)
        {
            return SnapshotFailure(FileSnapshotStatus.Denied, "The snapshot exceeds a configured host boundary.");
        }

        lock (_gate)
        {
            return !_files.TryGetValue(request.Path.Value, out var content)
                ? SnapshotFailure(FileSnapshotStatus.NotFound, "The snapshot target does not exist.")
                : content.Length > request.MaximumBytes
                ? SnapshotFailure(FileSnapshotStatus.LimitExceeded, "The snapshot target exceeds its byte bound.")
                : new FileSnapshotResult(
                    FileSnapshotStatus.Success, content, FileSecurityBinding.ContentFingerprint(content.AsSpan()), null);
        }
    }

    /// <inheritdoc/>
    private async ValueTask<AtomicFileReplaceResult> ReplaceCoreAsync(
        AtomicFileReplaceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfDefault(request.Content);
        ArgumentNullException.ThrowIfNull(request.Grant);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Content.Length > _maximumWriteBytes)
        {
            return ReplaceFailure(AtomicFileReplaceStatus.Denied, "The replacement exceeds a configured host boundary.");
        }

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.FileWrite,
            SecurityEffect.Replace,
            FileSecurityBinding.AtomicReplaceResources(request.Id, request.Path),
            FileSecurityBinding.AtomicReplaceFingerprint(
                request.Id, request.Path, request.ExpectedContentFingerprint, request.Content));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(request.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, intent))
        {
            return ReplaceFailure(AtomicFileReplaceStatus.Denied, FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        lock (_gate)
        {
            if (!_files.TryGetValue(request.Path.Value, out var current))
            {
                return ReplaceFailure(AtomicFileReplaceStatus.NotFound, "The replacement target does not exist.");
            }

            if (FileSecurityBinding.ContentFingerprint(current.AsSpan()) != request.ExpectedContentFingerprint)
            {
                return ReplaceFailure(AtomicFileReplaceStatus.Conflict, "The target changed after the edit was planned.");
            }

            _files[request.Path.Value] = request.Content;
            return new AtomicFileReplaceResult(
                AtomicFileReplaceStatus.Committed,
                FileSecurityBinding.ContentFingerprint(request.Content.AsSpan()),
                request.Content.Length,
                null);
        }
    }

    private static FileSnapshotResult SnapshotFailure(FileSnapshotStatus status, string message) => new(status, [], null, message);

    private static AtomicFileReplaceResult ReplaceFailure(AtomicFileReplaceStatus status, string message) => new(status, null, 0, message);
}

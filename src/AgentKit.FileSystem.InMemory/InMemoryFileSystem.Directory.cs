// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

public sealed partial class InMemoryFileSystem
{
    /// <inheritdoc/>
    private async ValueTask<DirectoryEnumerationResult> EnumerateCoreAsync(
        DirectoryEnumerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumEntries);
        ArgumentNullException.ThrowIfNull(request.Grant);
        cancellationToken.ThrowIfCancellationRequested();

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.DirectoryRead,
            SecurityEffect.Observe,
            [DirectorySecurityBinding.Resource(request.Path)],
            DirectorySecurityBinding.Fingerprint(request.Path, request.MaximumEntries, request.Continuation));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(request.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, intent))
        {
            return DirectoryFailure(DirectoryEnumerationStatus.Denied, FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        lock (_gate)
        {
            var directoryPath = request.Path?.Value;
            if (!DirectoryExists(directoryPath))
            {
                return DirectoryFailure(DirectoryEnumerationStatus.NotFound, "The directory does not exist.");
            }

            var childNames = new List<string>();
            foreach (var candidate in _directories.Concat(_files.Keys))
            {
                if (ParentDirectory(candidate) == directoryPath)
                {
                    childNames.Add(ChildName(candidate));
                    if (childNames.Count > _maximumDirectorySnapshotEntries)
                    {
                        return DirectoryFailure(
                            DirectoryEnumerationStatus.LimitExceeded,
                            $"The directory exceeds the configured snapshot limit of {_maximumDirectorySnapshotEntries} entries.");
                    }
                }
            }

            childNames.Sort(StringComparer.Ordinal);
            var childPaths = childNames
                .Select(name => directoryPath is null ? name : $"{directoryPath}/{name}")
                .ToList();
            var snapshot = SnapshotFingerprint(childPaths);
            var start = request.Continuation?.NextIndex ?? 0;
            if (request.Continuation is not null
                && (request.Continuation.SnapshotFingerprint != snapshot || start > childPaths.Count))
            {
                return DirectoryFailure(DirectoryEnumerationStatus.SnapshotChanged, "The directory changed after the supplied continuation was issued.");
            }

            var retained = childPaths.Skip(start).Take(request.MaximumEntries)
                .Select(static path => new DirectoryEntry(new FileSystemPath(path)))
                .ToImmutableArray();
            var next = start + retained.Length;
            var continuation = next < childPaths.Count ? new DirectoryEnumerationCursor(snapshot, next) : null;
            return new DirectoryEnumerationResult(DirectoryEnumerationStatus.Success, retained, snapshot, continuation, null);
        }
    }

    private static DirectoryEnumerationResult DirectoryFailure(DirectoryEnumerationStatus status, string message) =>
        new(status, [], null, null, message);

    /// <summary>Returns the last path segment.</summary>
    private static string ChildName(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? path : path[(separator + 1)..];
    }

    private static ContentHash SnapshotFingerprint(IEnumerable<string> paths)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var path in paths)
        {
            var bytes = Encoding.UTF8.GetBytes(path);
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
        }

        return new ContentHash($"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}");
    }
}

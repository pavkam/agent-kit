// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

public sealed partial class InMemoryFileSystem
{
    /// <inheritdoc/>
    public IAsyncEnumerable<FileSystemEntry> EnumerateAsync(
        AuthorizedDirectoryEnumeration operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return EnumerateObservedAsync(operation, cancellationToken);
    }

    private async IAsyncEnumerable<FileSystemEntry> EnumerateObservedAsync(
        AuthorizedDirectoryEnumeration operation,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var outcome = await ObserveValueTaskAsync(
            "enumerate", operation.Grant.RequestId, token => EnumerateCoreAsync(operation, token),
            static result => result.Status.ToString(), cancellationToken).ConfigureAwait(false);
        foreach (var entry in outcome.RequireEntries())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entry;
        }
    }

    /// <summary>Observes one directory after exact grant consumption.</summary>
    /// <param name="operation">The authorized enumeration evidence.</param>
    /// <param name="cancellationToken">Cancels before the observation settles.</param>
    /// <returns>The classified outcome; failures never throw except cancellation.</returns>
    private async ValueTask<DirectoryReadOutcome> EnumerateCoreAsync(
        AuthorizedDirectoryEnumeration operation,
        CancellationToken cancellationToken)
    {
        Debug.Assert(operation is not null, "The public boundary validates the operation.");
        cancellationToken.ThrowIfCancellationRequested();

        var directoryPath = VirtualPath(operation.ResolvedTarget) is "." ? null : VirtualPath(operation.ResolvedTarget);
        FileSystemPath? path = directoryPath is null ? null : new FileSystemPath(directoryPath);
        var enforcement = FileSystemEnforcementReceipt.Create(
            operation.Grant,
            SecurityAudience,
            SecurityOperationKind.DirectoryRead,
            SecurityEffect.Observe,
            [DirectorySecurityBinding.Resource(path)],
            DirectorySecurityBinding.Fingerprint(path));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(operation.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, operation.Grant, enforcement, intent))
        {
            return DirectoryReadOutcome.Failure(DirectoryReadStatus.Denied, FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        lock (_gate)
        {
            if (!DirectoryExists(directoryPath))
            {
                return DirectoryReadOutcome.Failure(DirectoryReadStatus.NotFound, "The directory does not exist.");
            }

            var children = new List<(string Name, bool IsDirectory)>();
            foreach (var candidate in _directories.Concat(_files.Keys))
            {
                if (ParentDirectory(candidate) == directoryPath)
                {
                    children.Add((ChildName(candidate), _directories.Contains(candidate)));
                    if (children.Count > _maximumDirectorySnapshotEntries)
                    {
                        return DirectoryReadOutcome.Failure(
                            DirectoryReadStatus.LimitExceeded,
                            $"The directory exceeds the configured snapshot limit of {_maximumDirectorySnapshotEntries} entries.");
                    }
                }
            }

            return DirectoryReadOutcome.Success([
                .. children
                    .OrderBy(static child => child.Name, StringComparer.Ordinal)
                    .Select(static child => new FileSystemEntry(new NormalizedRelativePath(child.Name), child.IsDirectory)),
            ]);
        }
    }

    /// <summary>Returns the last path segment.</summary>
    private static string ChildName(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? path : path[(separator + 1)..];
    }
}

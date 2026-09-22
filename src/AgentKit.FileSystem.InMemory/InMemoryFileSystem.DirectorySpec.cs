// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

public sealed partial class InMemoryFileSystem
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<FileSystemEntry> EnumerateAsync(
        AuthorizedDirectoryEnumeration operation,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var directoryPath = VirtualPath(operation.ResolvedTarget);
        var legacy = new DirectoryEnumerationRequest(
            directoryPath is null ? null : new FileSystemPath(directoryPath),
            maximumEntries: int.MaxValue,
            continuation: null,
            operation.Grant);
        var page = await EnumerateCoreAsync(legacy, cancellationToken).ConfigureAwait(false);
        if (page.Status is DirectoryEnumerationStatus.Denied)
        {
            throw new InvalidOperationException(page.SafeMessage ?? "Directory enumeration was denied.");
        }

        if (page.Status is DirectoryEnumerationStatus.NotFound)
        {
            yield break;
        }

        if (page.Status is not DirectoryEnumerationStatus.Success)
        {
            throw new InvalidOperationException(page.SafeMessage ?? "Directory enumeration failed.");
        }

        foreach (var entry in page.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = entry.Path.Value;
            bool isDirectory;
            lock (_gate)
            {
                isDirectory = _directories.Contains(path);
            }

            var slash = path.LastIndexOf('/');
            var relative = slash < 0 ? path : path[(slash + 1)..];
            yield return new FileSystemEntry(new NormalizedRelativePath(relative), isDirectory);
        }
    }
}

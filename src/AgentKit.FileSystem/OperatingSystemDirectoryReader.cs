// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Spec <see cref="IDirectoryReader"/> over the legacy page-based sandbox enumerator.</summary>
internal sealed class OperatingSystemDirectoryReader(ILegacyDirectoryReader legacyReader): IDirectoryReader
{
    private readonly ILegacyDirectoryReader _legacyReader = legacyReader;

    /// <inheritdoc/>
    public async IAsyncEnumerable<FileSystemEntry> EnumerateAsync(
        AuthorizedDirectoryEnumeration operation,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var path = LegacyPath(operation.ResolvedTarget);
        DirectoryEnumerationCursor? cursor = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _legacyReader.EnumerateAsync(
                new DirectoryEnumerationRequest(path, maximumEntries: 512, cursor, operation.Grant),
                cancellationToken).ConfigureAwait(false);
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
                var name = entry.Path.Value;
                var slash = name.LastIndexOf('/');
                var relative = slash < 0 ? name : name[(slash + 1)..];
                yield return new FileSystemEntry(new NormalizedRelativePath(relative), isDirectory: false);
            }

            if (page.Continuation is null)
            {
                yield break;
            }

            cursor = page.Continuation;
        }
    }

    private static FileSystemPath? LegacyPath(ResolvedFileTarget target)
    {
        var value = target.RelativePath.Value;
        return string.IsNullOrEmpty(value) ? null : new FileSystemPath(value);
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project.Tests;

using System.Text;

/// <summary>A scripted <see cref="IFileReader"/> over an in-memory path-to-bytes map that records every open request.</summary>
internal sealed class FakeFileReader: IFileReader
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.context.project.tests.reader");

    /// <summary>Gets every authorized read this reader was asked to open, in order.</summary>
    public List<AuthorizedFileRead> OpenedReads { get; } = [];

    /// <summary>Seeds UTF-8 text at a profile-relative path.</summary>
    /// <param name="path">The profile-relative path.</param>
    /// <param name="content">The text to expose.</param>
    internal void SeedText(string path, string content) => _files[path] = Encoding.UTF8.GetBytes(content);

    /// <summary>Seeds raw bytes at a profile-relative path.</summary>
    /// <param name="path">The profile-relative path.</param>
    /// <param name="content">The bytes to expose.</param>
    internal void SeedBytes(string path, byte[] content) => _files[path] = content;

    /// <inheritdoc/>
    public ValueTask<FileReadOpenResult> OpenReadAsync(AuthorizedFileRead operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        OpenedReads.Add(operation);
        if (!_files.TryGetValue(operation.Request.Target.Path.Value, out var bytes))
        {
            return ValueTask.FromResult<FileReadOpenResult>(new FileReadOpenNotFound());
        }

        var handle = new Handle(new FileMetadata(bytes.Length, null, null), new MemoryStream(bytes, writable: false));
        return ValueTask.FromResult<FileReadOpenResult>(new FileReadHandleOpened(handle));
    }

    private sealed class Handle(FileMetadata metadata, Stream content): IFileReadHandle
    {
        /// <inheritdoc/>
        public FileMetadata Metadata { get; } = metadata;

        /// <inheritdoc/>
        public Stream Content { get; } = content;

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Content.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

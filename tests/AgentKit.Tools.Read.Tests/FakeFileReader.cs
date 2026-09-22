// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

using System.Text;

/// <summary>A scripted <see cref="IFileReader"/> test double.</summary>
internal sealed class FakeFileReader: IFileReader
{
    /// <summary>Gets or sets the handler invoked for each open request.</summary>
    public Func<AuthorizedFileRead, FileReadOpenResult>? OnOpenRead { get; set; }

    /// <summary>Gets the open requests received by this double.</summary>
    public List<AuthorizedFileRead> ReceivedReads { get; } = [];

    /// <inheritdoc/>
    public ValueTask<FileReadOpenResult> OpenReadAsync(AuthorizedFileRead operation, CancellationToken cancellationToken = default)
    {
        ReceivedReads.Add(operation);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(OnOpenRead?.Invoke(operation) ?? new FileReadOpenFailed("not configured"));
    }

    /// <summary>Configures a handler that returns UTF-8 text content.</summary>
    /// <param name="text">The file text to expose.</param>
    /// <returns>This double with <see cref="OnOpenRead"/> configured.</returns>
    public FakeFileReader WithText(string text)
    {
        OnOpenRead = _ =>
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var stream = new MemoryStream(bytes, writable: false);
            var metadata = new FileMetadata(bytes.Length, null, null);
            return new FileReadHandleOpened(new FakeFileReadHandle(metadata, stream));
        };
        return this;
    }

    private sealed class FakeFileReadHandle(FileMetadata metadata, Stream stream): IFileReadHandle
    {
        /// <inheritdoc/>
        public FileMetadata Metadata { get; } = metadata;

        /// <inheritdoc/>
        public Stream Content { get; } = stream;

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            Content.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

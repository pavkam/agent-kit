// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Owns one bounded operating-system read stream and captured open metadata.</summary>
internal sealed class OperatingSystemFileReadHandle: IFileReadHandle
{
    private readonly OperatingSystemBoundedReadStream _stream;

    /// <summary>Initializes a new read handle over an opened regular file.</summary>
    /// <param name="metadata">Metadata captured at open time.</param>
    /// <param name="stream">The bounded content stream.</param>
    internal OperatingSystemFileReadHandle(FileMetadata metadata, OperatingSystemBoundedReadStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Metadata = metadata;
        _stream = stream;
        Content = stream;
    }

    /// <inheritdoc/>
    public FileMetadata Metadata { get; }

    /// <inheritdoc/>
    public Stream Content { get; }

    /// <summary>Gets whether the stream truncated due to growth after the opening stat.</summary>
    internal bool TruncatedDueToGrowth => _stream.TruncatedDueToGrowth;

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _stream.Dispose();
        return ValueTask.CompletedTask;
    }
}

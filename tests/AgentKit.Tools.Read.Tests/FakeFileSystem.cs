// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

/// <summary>A scripted <see cref="IFileSystem"/> test double for exercising read outcomes.</summary>
[Obsolete]
internal sealed class FakeFileSystem: IFileSystem
{
    public ComponentId SecurityAudience { get; } = new("test.filesystem");

    [Obsolete]
    public Func<LegacyFileReadRequest, FileReadResult>? OnRead { get; set; }

    [Obsolete]
    public Func<FileWriteRequest, LegacyFileWriteResult>? OnWrite { get; set; }

    [Obsolete]
    public List<LegacyFileReadRequest> ReceivedReads { get; } = [];

    public List<FileWriteRequest> ReceivedWrites { get; } = [];

    [Obsolete]
    public Task<FileReadResult> ReadAsync(LegacyFileReadRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedReads.Add(request);
        return Task.FromResult(OnRead?.Invoke(request) ?? new FileReadFailed("not configured"));
    }

    [Obsolete]
    public Task<LegacyFileWriteResult> WriteAsync(FileWriteRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedWrites.Add(request);
        return Task.FromResult(OnWrite?.Invoke(request) ?? new LegacyFileWriteFailed("not configured"));
    }
}

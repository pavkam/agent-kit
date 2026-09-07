// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read.Tests;

/// <summary>A scripted <see cref="IFileSystem"/> test double for exercising read outcomes.</summary>
internal sealed class FakeFileSystem: IFileSystem
{
    public Func<FileReadRequest, FileReadResult>? OnRead { get; set; }

    public Func<FileWriteRequest, FileWriteResult>? OnWrite { get; set; }

    public List<FileReadRequest> ReceivedReads { get; } = [];

    public List<FileWriteRequest> ReceivedWrites { get; } = [];

    public Task<FileReadResult> ReadAsync(FileReadRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedReads.Add(request);
        return Task.FromResult(OnRead?.Invoke(request) ?? new FileReadFailed("not configured"));
    }

    public Task<FileWriteResult> WriteAsync(FileWriteRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedWrites.Add(request);
        return Task.FromResult(OnWrite?.Invoke(request) ?? new FileWriteFailed("not configured"));
    }
}

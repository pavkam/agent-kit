// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project.Tests;

using System.Text;

/// <summary>Minimal file-system stub for project instruction tests.</summary>
internal sealed class StubFileSystem: IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public ComponentId SecurityAudience { get; } = new("agentkit.filesystem.stub");

    internal void Seed(string path, string content) => _files[path] = content;

    public Task<FileReadResult> ReadAsync(FileReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var key = request.Path.Value;
        return Task.FromResult<FileReadResult>(_files.TryGetValue(key, out var content)
            ? new FileRead(content, Encoding.UTF8.GetByteCount(content))
            : new FileNotFound(request.Path));
    }

    public Task<FileWriteResult> WriteAsync(FileWriteRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

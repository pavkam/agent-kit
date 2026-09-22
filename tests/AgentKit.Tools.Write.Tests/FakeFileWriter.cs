// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write.Tests;

using System.Text;

/// <summary>A scripted <see cref="IFileWriter"/> test double.</summary>
internal sealed class FakeFileWriter: IFileWriter
{
    /// <summary>Gets or sets the handler invoked for each write.</summary>
    public Func<AuthorizedFileWrite, FileWriteContent, FileWriteResult>? OnWrite { get; set; }

    /// <summary>Gets the write operations received by this double.</summary>
    public List<(AuthorizedFileWrite Operation, FileWriteContent Content)> ReceivedWrites { get; } = [];

    /// <inheritdoc/>
    public ValueTask<FileWriteResult> WriteAsync(
        AuthorizedFileWrite operation,
        FileWriteContent content,
        CancellationToken cancellationToken = default)
    {
        ReceivedWrites.Add((operation, content));
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(OnWrite?.Invoke(operation, content) ?? new FileWriteFailed("not configured"));
    }

    /// <summary>Configures a handler that reports success for the payload size.</summary>
    /// <returns>This double with <see cref="OnWrite"/> configured.</returns>
    public FakeFileWriter WithSuccess()
    {
        OnWrite = static (operation, content) =>
        {
            var payloadFingerprint = FileSecurityBinding.ContentFingerprint(content.Payload.Span);
            return new FileWriteSuccess(
                FileWriteOutcomeKind.Created,
                content.Payload.Length,
                0,
                content.Payload.Length,
                payloadFingerprint,
                payloadFingerprint);
        };
        return this;
    }

    /// <summary>Gets the UTF-8 text from the last received write, if any.</summary>
    public string? LastWrittenText =>
        ReceivedWrites.Count == 0
            ? null
            : Encoding.UTF8.GetString(ReceivedWrites[^1].Content.Payload.Span);
}

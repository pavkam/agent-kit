// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileWriteFailed behavior and contracts.</summary>
public sealed class FileWriteFailedTests: Conformance.SingleMessageLeafConformanceTests<FileWriteFailed>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FileWriteFailed_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) => _ = Should.Throw<ArgumentException>(() => new FileWriteFailed(safeMessage!));
    [Fact]
    public void FileWriteFailed_Constructor_WhenValid_RoundTripsSafeMessage()
    {
        var failed = new FileWriteFailed("disk error");
        failed.SafeMessage.ShouldBe("disk error");
    }

    /// <inheritdoc/>
    protected override FileWriteFailed Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(FileWriteFailed subject) => subject.SafeMessage;
}

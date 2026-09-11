// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileReadFailed behavior and contracts.</summary>
public sealed class FileReadFailedTests: Conformance.SingleMessageLeafConformanceTests<FileReadFailed>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FileReadFailed_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) => _ = Should.Throw<ArgumentException>(() => new FileReadFailed(safeMessage!));
    [Fact]
    public void FileReadFailed_Constructor_WhenValid_RoundTripsSafeMessage()
    {
        var failed = new FileReadFailed("disk error");
        failed.SafeMessage.ShouldBe("disk error");
    }

    /// <inheritdoc/>
    protected override FileReadFailed Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(FileReadFailed subject) => subject.SafeMessage;
}

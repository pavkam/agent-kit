// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileReadDenied behavior and contracts.</summary>
public sealed class FileReadDeniedTests: Conformance.SingleMessageLeafConformanceTests<FileReadDenied>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FileReadDenied_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) => _ = Should.Throw<ArgumentException>(() => new FileReadDenied(safeMessage!));
    [Fact]
    public void FileReadDenied_Constructor_WhenValid_RoundTripsSafeMessage()
    {
        var denied = new FileReadDenied("outside sandbox");
        denied.SafeMessage.ShouldBe("outside sandbox");
    }

    /// <inheritdoc/>
    protected override FileReadDenied Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(FileReadDenied subject) => subject.SafeMessage;
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileWriteDenied behavior and contracts.</summary>
public sealed class FileWriteDeniedTests: Conformance.SingleMessageLeafConformanceTests<FileWriteDenied>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FileWriteDenied_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) => _ = Should.Throw<ArgumentException>(() => new FileWriteDenied(safeMessage!));
    [Fact]
    public void FileWriteDenied_Constructor_WhenValid_RoundTripsSafeMessage()
    {
        var denied = new FileWriteDenied("too large");
        denied.SafeMessage.ShouldBe("too large");
    }

    [Fact]
    public void FileWriteDenied_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new FileWriteDenied("too large");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    /// <inheritdoc/>
    protected override FileWriteDenied Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(FileWriteDenied subject) => subject.SafeMessage;
}

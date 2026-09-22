// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies LegacyFileWriteFailed behavior and contracts.</summary>
[Obsolete]
public sealed class FileWriteFailedTests: Conformance.SingleMessageLeafConformanceTests<LegacyFileWriteFailed>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Obsolete]
    public void FileWriteFailed_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) => _ = Should.Throw<ArgumentException>(() => new LegacyFileWriteFailed(safeMessage!));
    [Fact]
    [Obsolete]
    public void FileWriteFailed_Constructor_WhenValid_RoundTripsSafeMessage()
    {
        var failed = new LegacyFileWriteFailed("disk error");
        failed.SafeMessage.ShouldBe("disk error");
    }

    [Fact]
    [Obsolete]
    public void FileWriteFailed_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LegacyFileWriteFailed("disk error");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    /// <inheritdoc/>
    [Obsolete]
    protected override LegacyFileWriteFailed Create(string message) => new(message);

    /// <inheritdoc/>
    [Obsolete]
    protected override string GetValue(LegacyFileWriteFailed subject) => subject.SafeMessage;
}

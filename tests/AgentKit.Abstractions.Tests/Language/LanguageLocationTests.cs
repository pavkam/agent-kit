// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguageLocation behavior and contracts.</summary>
public sealed class LanguageLocationTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var path = new FileSystemPath("src/a.cs");
        var range = Range();
        var fingerprint = new ContentHash("sha256:test");
        var location = new LanguageLocation(path, range, fingerprint);
        location.Path.ShouldBe(path);
        location.Range.ShouldBe(range);
        location.DocumentFingerprint.ShouldBe(fingerprint);
    }

    [Fact]
    public void Constructor_WhenDocumentFingerprintIsNull_InitializesNull()
    {
        var location = new LanguageLocation(new FileSystemPath("src/a.cs"), Range(), null);
        location.DocumentFingerprint.ShouldBeNull();
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var path = new FileSystemPath("src/a.cs");
        var first = new LanguageLocation(path, Range(), null);
        var second = new LanguageLocation(path, Range(), null);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LanguageLocation(new FileSystemPath("src/a.cs"), Range(), null);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static LanguageRange Range() => new(new LanguagePosition(0, 0), new LanguagePosition(0, 5));
}

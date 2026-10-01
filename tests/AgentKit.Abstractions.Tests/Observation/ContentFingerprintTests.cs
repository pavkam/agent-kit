// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Observation;

/// <summary>Verifies <see cref="ContentFingerprint"/> validation and value semantics.</summary>
public sealed class ContentFingerprintTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void Constructor_WhenValueIsBlank_ThrowsArgumentExceptionNamingValue(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new ContentFingerprint(value!));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueIsProvided_PreservesTextAndEquality()
    {
        var fingerprint = new ContentFingerprint("sha256:abc");

        fingerprint.Value.ShouldBe("sha256:abc");
        fingerprint.ToString().ShouldBe("sha256:abc");
        fingerprint.ShouldBe(new ContentFingerprint("sha256:abc"));
    }
}

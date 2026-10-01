// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="CandidateContent"/> constraints and measurement.</summary>
public sealed class CandidateContentTests
{
    [Fact]
    public void Constructor_WhenTextIsSupplied_MeasuresItsUtf8Size()
    {
        var content = new CandidateContent("héllo");

        content.Text.ShouldBe("héllo");
        content.Utf8Bytes.ShouldBe(6);
    }

    [Fact]
    public void Constructor_WhenTextIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new CandidateContent(null!)).ParamName.ShouldBe("text");

    [Fact]
    public void Constructor_WhenTextIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new CandidateContent(" ")).ParamName.ShouldBe("text");
}

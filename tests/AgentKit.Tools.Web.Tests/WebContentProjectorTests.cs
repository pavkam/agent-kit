// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Web.Tests;

public sealed class WebContentProjectorTests
{
    [Fact]
    public void Project_WhenUtf8Invalid_ThrowsTypedContentFailure()
    {
        var action = () => WebContentProjector.Project([0xff], "text/plain; charset=utf-8", 100);

        _ = action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Project_WhenBinaryNulPresent_RejectsBeforeDecoding()
    {
        var action = () => WebContentProjector.Project([0x41, 0x00, 0x42], "text/plain", 100);

        _ = action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Project_WhenBomPresent_UsesBomAndRemovesItFromText()
    {
        var projection = WebContentProjector.Project(
            [0xef, 0xbb, 0xbf, 0x68, 0x69],
            "text/plain",
            100);

        projection.Text.ShouldBe("hi");
        projection.Encoding.ShouldBe("utf-8");
    }
}

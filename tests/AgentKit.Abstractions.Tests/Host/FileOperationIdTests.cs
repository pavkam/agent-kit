// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies FileOperationId behavior and contracts.</summary>
public sealed class FileOperationIdTests
{
    [Fact]
    public void Constructor_WhenValueEmpty_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileOperationId(Guid.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueValid_RoundTripsValue()
    {
        var id = new FileOperationId(Guid.Parse("21000000-0000-0000-0000-000000000001"));
        id.Value.ShouldBe(Guid.Parse("21000000-0000-0000-0000-000000000001"));
    }
}

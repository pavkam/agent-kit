// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="McpEndpointBounds"/>.</summary>
public sealed class McpEndpointBoundsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Constructor_WhenTimeoutIsNotPositive_ThrowsArgumentOutOfRangeException(int invalidMember)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new McpEndpointBounds(
            invalidMember == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(1),
            invalidMember == 1 ? TimeSpan.Zero : TimeSpan.FromSeconds(1),
            invalidMember == 2 ? TimeSpan.MinValue : TimeSpan.FromSeconds(1),
            1,
            1,
            1));

        exception.ParamName.ShouldBe(invalidMember switch
        {
            0 => "handshakeTimeout",
            1 => "requestTimeout",
            _ => "shutdownTimeout",
        });
    }

    [Theory]
    [InlineData(0, "maximumFrameBytes")]
    [InlineData(1, "maximumMessageBytes")]
    [InlineData(2, "maximumInFlightRequests")]
    public void Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException(int invalidMember, string expectedParamName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new McpEndpointBounds(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            invalidMember == 0 ? 0 : 1,
            invalidMember == 1 ? -1 : 1,
            invalidMember == 2 ? 0 : 1));

        exception.ParamName.ShouldBe(expectedParamName);
    }

    [Fact]
    public void Constructor_WhenBoundsArePositive_PreservesThem()
    {
        var bounds = McpContractTestData.Bounds();
        bounds.HandshakeTimeout.ShouldBe(TimeSpan.FromSeconds(15));
        bounds.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(60));
        bounds.ShutdownTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        bounds.MaximumFrameBytes.ShouldBe(1_048_576);
        bounds.MaximumMessageBytes.ShouldBe(4_194_304);
        bounds.MaximumInFlightRequests.ShouldBe(16);
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

/// <summary>Verifies argument validation at the bounded materialized-JSON serialization boundary.</summary>
public sealed class BoundedJsonSerializerTests
{
    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void TryComputeHash_WhenDepthReachesFixedLimit_EnforcesInclusiveBound(int depth, bool expected)
    {
        var json = new string('[', depth - 1) + "0" + new string(']', depth - 1);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = depth });

        var result = BoundedJsonSerializer.TryComputeHash(
            document.RootElement, 4096, TestContext.Current.CancellationToken, out var hash);

        result.ShouldBe(expected);
        if (!expected)
        {
            hash.ShouldBe(default);
        }
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, false)]
    public void TryComputeHash_WhenBytesReachLimit_EnforcesInclusiveBound(int maximumBytes, bool expected)
    {
        var result = BoundedJsonSerializer.TryComputeHash(
            TestFactory.ParseJson("null"), maximumBytes, TestContext.Current.CancellationToken, out var hash);

        result.ShouldBe(expected);
        if (!expected)
        {
            hash.ShouldBe(default);
        }
    }

    [Fact]
    public void TryComputeHash_WhenCancelled_PropagatesCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        var exception = Should.Throw<OperationCanceledException>(() =>
            BoundedJsonSerializer.TryComputeHash(TestFactory.ParseJson("null"), 4, source.Token, out _));

        exception.CancellationToken.ShouldBe(source.Token);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryComputeHash_WhenMaximumBytesIsNotPositive_ThrowsBeforeSerialization(int maximumBytes)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => BoundedJsonSerializer.TryComputeHash(
                TestFactory.ParseJson("null"),
                maximumBytes,
                TestContext.Current.CancellationToken,
                out _));

        exception.ParamName.ShouldBe("maximumBytes");
    }

    [Fact]
    public void TryComputeHash_WhenValueIsUndefined_ThrowsBeforeHashAllocation()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => BoundedJsonSerializer.TryComputeHash(
                default,
                1,
                TestContext.Current.CancellationToken,
                out _));

        exception.ParamName.ShouldBe("value");
    }
}

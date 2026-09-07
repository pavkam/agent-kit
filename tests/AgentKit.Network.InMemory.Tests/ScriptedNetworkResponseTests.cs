// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

public sealed class ScriptedNetworkResponseTests
{
    [Fact]
    public void Constructor_WhenMetadataIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ScriptedNetworkResponse(null!, ReadOnlyMemory<byte>.Empty));

        exception.ParamName.ShouldBe("metadata");
    }

    [Fact]
    public async Task Content_ReturnsScriptedBytes()
    {
        var response = new ScriptedNetworkResponse(TestFactory.Metadata(), "hello"u8.ToArray());

        using var reader = new StreamReader(response.Content);
        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        text.ShouldBe("hello");
        await response.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_IsIdempotent()
    {
        var response = new ScriptedNetworkResponse(TestFactory.Metadata(), ReadOnlyMemory<byte>.Empty);

        await response.DisposeAsync();
        await response.DisposeAsync();
    }

    [Fact]
    public void Metadata_ReturnsSuppliedMetadata()
    {
        var metadata = TestFactory.Metadata(404);
        var response = new ScriptedNetworkResponse(metadata, ReadOnlyMemory<byte>.Empty);

        response.Metadata.ShouldBe(metadata);
    }
}

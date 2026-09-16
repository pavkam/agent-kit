// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

/// <summary>Verifies ScriptedNetworkResponse behavior and contracts.</summary>
public sealed class ScriptedNetworkResponseTests
{
    [Fact]
    public void Constructor_WhenMetadataIsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ScriptedNetworkResponse(null!, ReadOnlyMemory<byte>.Empty));
        exception.ParamName.ShouldBe("metadata");
    }

    [Fact]
    public async Task Constructor_WhenContentProvided_ExposesMetadataAndCopiedBody()
    {
        var metadata = new NetworkResponseMetadata(200, NetworkHeaderSet.Empty, 5);
        var response = new ScriptedNetworkResponse(metadata, "hello"u8.ToArray());
        response.Metadata.ShouldBeSameAs(metadata);
        using var reader = new StreamReader(response.Content);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("hello");
        await response.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_DisposesOwnedContentStream()
    {
        var response = new ScriptedNetworkResponse(new NetworkResponseMetadata(200, NetworkHeaderSet.Empty, null), ReadOnlyMemory<byte>.Empty);
        var content = response.Content;
        await response.DisposeAsync();
        _ = Should.Throw<ObjectDisposedException>(() => content.ReadByte());
    }
}

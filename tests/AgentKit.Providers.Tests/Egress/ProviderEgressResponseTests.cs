// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests.Egress;

using System.Net;

using AgentKit.Providers.Egress;
using AgentKit.TestSupport;

/// <summary>Verifies <see cref="ProviderEgressResponse"/> status, header projection, ownership, and disposal.</summary>
public sealed class ProviderEgressResponseTests
{
    [Fact]
    public async Task Constructor_WhenNetworkResponseCarriesHeaders_SplitsMessageAndContentHeaders()
    {
        await using var response = new ProviderEgressResponse(new StreamNetworkResponse(
            429,
            new MemoryStream([1, 2, 3]),
            new NetworkHeader("Retry-After", "7"),
            new NetworkHeader("x-request-id", "req-9"),
            new NetworkHeader("Content-Type", "application/json")));

        response.StatusCode.ShouldBe((HttpStatusCode) 429);
        response.IsSuccessStatusCode.ShouldBeFalse();
        response.Headers.RetryAfter!.Delta.ShouldBe(TimeSpan.FromSeconds(7));
        response.Headers.GetValues("x-request-id").ShouldBe(["req-9"]);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task Constructor_WhenStatusIsSuccess_ExposesTheBodyStreamWithoutBuffering()
    {
        await using var response = new ProviderEgressResponse(new StreamNetworkResponse(200, new ChunkedStream([9, 8, 7, 6], 1)));

        var body = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        var buffer = new byte[4];
        var first = await body.ReadAsync(buffer, TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.ShouldBeTrue();
        first.ShouldBe(1);
        buffer[0].ShouldBe((byte) 9);
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_ReleasesTheNetworkResponseOnce()
    {
        var network = new StreamNetworkResponse(200, new MemoryStream([1]));
        var response = new ProviderEgressResponse(network);

        await response.DisposeAsync();
        await response.DisposeAsync();

        network.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenNetworkResponseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ProviderEgressResponse(null!));

        exception.ParamName.ShouldBe("network");
    }
}

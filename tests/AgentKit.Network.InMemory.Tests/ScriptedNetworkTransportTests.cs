// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network.InMemory.Tests;

public sealed class ScriptedNetworkTransportTests
{
    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ScriptedNetworkTransport(null!));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public async Task SendAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var transport = TestFactory.Transport();

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => transport.SendAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task SendAsync_WhenUnscripted_ReturnsRequestFailed()
    {
        var transport = TestFactory.Transport();

        var result = await transport.SendAsync(TestFactory.Request(), TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<NetworkRequestFailed>();
        failed.SideEffectCertain.ShouldBeTrue();
    }

    [Fact]
    public async Task SendAsync_WhenScripted_ReturnsScriptedResult()
    {
        var transport = TestFactory.Transport();
        var destination = TestFactory.Destination();
        var response = new ScriptedNetworkResponse(TestFactory.Metadata(), "hello"u8.ToArray());
        var scripted = new NetworkResponseReceived(response);
        transport.Script(NetworkMethod.Get, destination, scripted);

        var result = await transport.SendAsync(TestFactory.Request(destination), TestContext.Current.CancellationToken);

        result.ShouldBe(scripted);
    }

    [Fact]
    public async Task SendAsync_WhenScriptedForDifferentMethod_DoesNotMatch()
    {
        var transport = TestFactory.Transport();
        var destination = TestFactory.Destination();
        var response = new ScriptedNetworkResponse(TestFactory.Metadata(), ReadOnlyMemory<byte>.Empty);
        transport.Script(NetworkMethod.Post, destination, new NetworkResponseReceived(response));

        var result = await transport.SendAsync(
            TestFactory.Request(destination, NetworkMethod.Get), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkRequestFailed>();
    }

    [Fact]
    public async Task SendAsync_WhenPolicyRejectsDestination_ReturnsDeniedWithoutRecordingTraceOrConsultingScript()
    {
        var policy = new NetworkDestinationPolicy(["https"], [new NormalizedHost("allowed.example.com")], allowPrivateAddresses: false);
        var transport = TestFactory.Transport(policy: policy);
        var destination = TestFactory.Destination("blocked.example.com");
        transport.Script(NetworkMethod.Get, destination, new NetworkResponseReceived(new ScriptedNetworkResponse(TestFactory.Metadata(), ReadOnlyMemory<byte>.Empty)));

        var result = await transport.SendAsync(TestFactory.Request(destination), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<NetworkDenied>();
        transport.Traces.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenPolicyAllowsDestination_ConsultsScript()
    {
        var policy = new NetworkDestinationPolicy(["https"], null, allowPrivateAddresses: false);
        var transport = TestFactory.Transport(policy: policy);
        var destination = TestFactory.Destination();
        var scripted = new NetworkResponseReceived(new ScriptedNetworkResponse(TestFactory.Metadata(), ReadOnlyMemory<byte>.Empty));
        transport.Script(NetworkMethod.Get, destination, scripted);

        var result = await transport.SendAsync(TestFactory.Request(destination), TestContext.Current.CancellationToken);

        result.ShouldBe(scripted);
    }

    [Fact]
    public void Script_WhenNoResultsSupplied_ThrowsArgumentException()
    {
        var transport = TestFactory.Transport();

        _ = Should.Throw<ArgumentException>(() => transport.Script(NetworkMethod.Get, TestFactory.Destination()));
    }

    [Fact]
    public async Task SendAsync_AlwaysRecordsTrace_EvenWhenUnscripted()
    {
        var transport = TestFactory.Transport();
        var request = TestFactory.Request();

        _ = await transport.SendAsync(request, TestContext.Current.CancellationToken);

        transport.Traces.Count.ShouldBe(1);
        transport.Traces[0].Id.ShouldBe(request.Id);
        transport.Traces[0].Phase.ShouldBe("send");
    }
}

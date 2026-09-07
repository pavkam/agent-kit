// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

public sealed class McpToolContractTests
{
    [Fact]
    public void Create_WhenClassHasAttributedMethod_DescribesObjectRequestAndResponse()
    {
        var contract = new McpToolContract<WeatherTools>();

        var method = contract.Methods.ShouldHaveSingleItem();
        method.Name.ShouldBe(new McpToolName("weather.get"));
        method.Version.ShouldBe(new ToolVersion("2.1"));
        method.RequestType.ShouldBe(typeof(WeatherRequest));
        method.ResponseType.ShouldBe(typeof(WeatherResponse));
        method.RequestParameterName.ShouldBe("request");
        method.Description.ShouldBe("Gets the forecast for one city.");
    }

    [Fact]
    public void Create_WhenRequestIsPrimitive_RejectsContract()
    {
        var exception = Should.Throw<InvalidOperationException>(static () => new McpToolContract<PrimitiveRequestTools>());

        exception.Message.ShouldContain("request object");
    }

    [Fact]
    public void Create_WhenReturnIsSynchronous_RejectsContract()
    {
        var exception = Should.Throw<InvalidOperationException>(static () => new McpToolContract<SynchronousTools>());

        exception.Message.ShouldContain("Task<TResponse> or ValueTask<TResponse>");
    }

    [Fact]
    public void Create_WhenNamesCollide_RejectsContract()
    {
        var exception = Should.Throw<InvalidOperationException>(static () => new McpToolContract<DuplicateTools>());

        exception.Message.ShouldContain("duplicate tool name");
    }

    [Fact]
    public void AddMcpToolContract_WhenCalledTwice_RegistersOneContract()
    {
        var services = new ServiceCollection();

        _ = services.AddMcpToolContract<WeatherTools>();
        _ = services.AddMcpToolContract<WeatherTools>();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<McpToolContract<WeatherTools>>().ShouldHaveSingleItem();
    }

    private abstract class WeatherTools
    {
        [McpTool("weather.get", "2.1")]
        [Description("Gets the forecast for one city.")]
        public abstract Task<WeatherResponse> GetAsync(
            WeatherRequest request,
            CancellationToken cancellationToken = default);
    }

    private abstract class PrimitiveRequestTools
    {
        [McpTool("primitive", "1")]
        public abstract Task<WeatherResponse> GetAsync(
            string request,
            CancellationToken cancellationToken = default);
    }

    private abstract class SynchronousTools
    {
        [McpTool("sync", "1")]
        public abstract WeatherResponse Get(WeatherRequest request);
    }

    private abstract class DuplicateTools
    {
        [McpTool("duplicate", "1")]
        public abstract Task<WeatherResponse> FirstAsync(WeatherRequest request);

        [McpTool("duplicate", "2")]
        public abstract Task<WeatherResponse> SecondAsync(WeatherRequest request);
    }

    private sealed record WeatherRequest(string City);

    private sealed record WeatherResponse(string Forecast);
}

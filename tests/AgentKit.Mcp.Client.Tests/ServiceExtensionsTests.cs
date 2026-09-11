// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddMcpToolClient_WhenServicesAreNull_ThrowsForServices()
    {
        IServiceCollection services = null!;
        IServiceCollection Act() => services.AddMcpToolClient<SimpleTools>();
        var exception = Should.Throw<ArgumentNullException>(Act);
        exception.ParamName.ShouldBe("services");
    }

    private abstract class SimpleTools
    {
        [McpTool("simple", "1")]
        public abstract Task<SimpleResponse> ExecuteAsync(SimpleRequest request);
    }

    private sealed record SimpleRequest(string Value);
    private sealed record SimpleResponse(string Value);
    [Fact]
    public void AddMcpToolClient_WhenCalledTwice_RegistersOneFactoryAndContract()
    {
        var services = new ServiceCollection();
        _ = services.AddMcpToolClient<WeatherTools>();
        _ = services.AddMcpToolClient<WeatherTools>();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetServices<McpToolClientFactory<WeatherTools>>().ShouldHaveSingleItem();
        _ = provider.GetServices<McpToolContract<WeatherTools>>().ShouldHaveSingleItem();
    }

    private abstract class WeatherTools
    {
        [McpTool("weather.get", "2.1")]
        public abstract Task<WeatherResponse> GetAsync(WeatherRequest request, CancellationToken cancellationToken = default);
    }

    private sealed record WeatherRequest(string City);
    private sealed record WeatherResponse(string Forecast);
}

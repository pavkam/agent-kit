// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;
/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
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
        public abstract Task<WeatherResponse> GetAsync(WeatherRequest request, CancellationToken cancellationToken = default);
    }

    private sealed record WeatherRequest(string City);
    private sealed record WeatherResponse(string Forecast);
}

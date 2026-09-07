// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void WithAgentKitTools_WhenClassProvided_ReflectsSchemaAndVersionMetadata()
    {
        var services = new ServiceCollection();
        var builder = services.AddAgentKitMcpServer();

        _ = builder.WithAgentKitTools<WeatherTools>();
        using var provider = services.BuildServiceProvider();

        var tool = provider.GetServices<McpServerTool>().ShouldHaveSingleItem().ProtocolTool;
        tool.Name.ShouldBe("weather.get");
        tool.Description.ShouldBe("Gets the forecast for one city.");
        tool.InputSchema.GetProperty("properties").TryGetProperty("request", out _).ShouldBeTrue();
        tool.InputSchema.GetProperty("required").EnumerateArray().Select(static item => item.GetString())
            .ShouldContain("request");
        tool.OutputSchema.ShouldNotBeNull().GetProperty("type").GetString().ShouldBe("object");
        tool.Meta.ShouldNotBeNull()[McpMetadataKeys.ToolContractVersion]!.GetValue<string>().ShouldBe("2.1");
        tool.Annotations.ShouldNotBeNull().ReadOnlyHint.ShouldBe(true);
        tool.Annotations.DestructiveHint.ShouldBe(true);
        tool.Annotations.OpenWorldHint.ShouldBe(true);
        tool.Annotations.IdempotentHint.ShouldBe(false);
    }

    [Fact]
    public void WithAgentKitTools_WhenSeveralMethodsProvided_RegistersEveryToolInContractOrder()
    {
        var services = new ServiceCollection();
        var builder = services.AddAgentKitMcpServer();

        _ = builder.WithAgentKitTools<MultipleTools>();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<McpServerTool>()
            .Select(static tool => tool.ProtocolTool.Name)
            .ShouldBe(["first", "second"]);
    }

    [Fact]
    public void WithAgentKitTools_WhenCustomSerializerProvided_UsesItForNestedSchemas()
    {
        var services = new ServiceCollection();
        var builder = services.AddAgentKitMcpServer();
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        _ = builder.WithAgentKitTools<SerializerTools>(options);
        using var provider = services.BuildServiceProvider();

        var tool = provider.GetServices<McpServerTool>().ShouldHaveSingleItem().ProtocolTool;
        var requestSchema = tool.InputSchema.GetProperty("properties").GetProperty("request");
        requestSchema.GetProperty("properties").TryGetProperty("postal_code", out _).ShouldBeTrue();
        tool.OutputSchema.ShouldNotBeNull().GetProperty("properties")
            .TryGetProperty("forecast_text", out _).ShouldBeTrue();
        options.TypeInfoResolver.ShouldBeNull();
    }

    [Fact]
    public void WithAgentKitTools_WhenAllEffectHintsAreSafe_MapsEachAnnotationIndependently()
    {
        var services = new ServiceCollection();
        var builder = services.AddAgentKitMcpServer();

        _ = builder.WithAgentKitTools<SafeTools>();
        using var provider = services.BuildServiceProvider();

        var annotations = provider.GetServices<McpServerTool>()
            .ShouldHaveSingleItem().ProtocolTool.Annotations.ShouldNotBeNull();
        annotations.ReadOnlyHint.ShouldBe(true);
        annotations.IdempotentHint.ShouldBe(true);
        annotations.OpenWorldHint.ShouldBe(false);
        annotations.DestructiveHint.ShouldBe(false);
    }

    [Fact]
    public void WithAgentKitTools_WhenToolClassIsAbstract_RejectsBeforeRegistration()
    {
        var services = new ServiceCollection();
        var builder = services.AddAgentKitMcpServer();

        var exception = Should.Throw<InvalidOperationException>(
            () => builder.WithAgentKitTools<AbstractTools>());

        exception.Message.ShouldContain("must be concrete");
        services.BuildServiceProvider().GetServices<McpServerTool>().ShouldBeEmpty();
    }

    [Fact]
    public void WithAgentKitTools_WhenContractShapeIsInvalid_RejectsBeforeRegistration()
    {
        var services = new ServiceCollection();
        var builder = services.AddAgentKitMcpServer();

        var exception = Should.Throw<InvalidOperationException>(
            () => builder.WithAgentKitTools<InvalidTools>());

        exception.Message.ShouldContain("request object");
        services.BuildServiceProvider().GetServices<McpServerTool>().ShouldBeEmpty();
    }

    [Fact]
    public void WithAgentKitTools_WhenBuilderIsNull_ThrowsForBuilder()
    {
        IMcpServerBuilder builder = null!;

        IMcpServerBuilder Act() => builder.WithAgentKitTools<WeatherTools>();

        var exception = Should.Throw<ArgumentNullException>(Act);
        exception.ParamName.ShouldBe("builder");
    }

    [Fact]
    public void AddAgentKitMcpServer_WhenVersionRequired_ConfiguresExactProtocolVersion()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentKitMcpServer(McpServerVersionPolicy.Require(McpProtocolVersions.November2025));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<McpServerOptions>>().Value.ProtocolVersion
            .ShouldBe("2025-11-25");
    }

    [Fact]
    public void AddAgentKitMcpServer_WhenPolicyIsOmitted_LeavesSdkCompatibilityNegotiationEnabled()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentKitMcpServer();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<McpServerOptions>>().Value.ProtocolVersion.ShouldBeNull();
    }

    [Theory]
    [InlineData("2024-11-05")]
    [InlineData("2025-03-26")]
    [InlineData("2025-06-18")]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public void AddAgentKitMcpServer_WhenKnownVersionRequired_ConfiguresExactRevision(string value)
    {
        var services = new ServiceCollection();

        _ = services.AddAgentKitMcpServer(McpServerVersionPolicy.Require(McpProtocolVersion.Parse(value)));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<McpServerOptions>>().Value.ProtocolVersion.ShouldBe(value);
    }

    [Fact]
    public void AddAgentKitMcpServer_WhenServicesAreNull_ThrowsForServices()
    {
        IServiceCollection services = null!;

        IMcpServerBuilder Act() => services.AddAgentKitMcpServer();

        var exception = Should.Throw<ArgumentNullException>(Act);
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void Require_WhenVersionIsUninitialized_ThrowsForVersion()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => McpServerVersionPolicy.Require(default));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Compatible_WhenRead_HasNoRequiredVersion() =>
        McpServerVersionPolicy.Compatible.RequiredVersion.ShouldBeNull();

    [Theory]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public void Require_WhenVersionIsInitialized_CapturesExactVersion(string value)
    {
        var version = McpProtocolVersion.Parse(value);

        var policy = McpServerVersionPolicy.Require(version);

        policy.RequiredVersion.ShouldBe(version);
    }

    private abstract class WeatherContract
    {
        [McpTool("weather.get", "2.1", ReadOnly = true)]
        [Description("Gets the forecast for one city.")]
        public abstract Task<WeatherResponse> GetAsync(
            WeatherRequest request,
            CancellationToken cancellationToken = default);
    }

    private sealed class WeatherTools: WeatherContract
    {
        private readonly string _condition = "Sunny";

        public override Task<WeatherResponse> GetAsync(
            WeatherRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WeatherResponse($"{_condition} in {request.City}"));
        }
    }

    private sealed record WeatherRequest(string City);

    private sealed record WeatherResponse(string Forecast);

#pragma warning disable CA1822 // Instance methods are required to exercise the reflected server contract.
    private sealed class MultipleTools
    {
        [McpTool("first", "1")]
        public Task<WeatherResponse> FirstAsync(WeatherRequest request) =>
            Task.FromResult(new WeatherResponse(request.City));

        [McpTool("second", "2")]
        public ValueTask<WeatherResponse> SecondAsync(WeatherRequest request) =>
            ValueTask.FromResult(new WeatherResponse(request.City));
    }

    private sealed class SerializerTools
    {
        [McpTool("serializer", "1")]
        public Task<SerializerResponse> ExecuteAsync(SerializerRequest request) =>
            Task.FromResult(new SerializerResponse(request.PostalCode));
    }

    private sealed class SafeTools
    {
        [McpTool("safe", "1", ReadOnly = true, Idempotent = true, OpenWorld = false, Destructive = false)]
        public Task<WeatherResponse> ExecuteAsync(WeatherRequest request) =>
            Task.FromResult(new WeatherResponse(request.City));
    }

    private abstract class AbstractTools
    {
        [McpTool("abstract", "1")]
        public abstract Task<WeatherResponse> ExecuteAsync(WeatherRequest request);
    }

    private sealed class InvalidTools
    {
        [McpTool("invalid", "1")]
        public Task<WeatherResponse> ExecuteAsync(WeatherRequest[] request) =>
            Task.FromResult(new WeatherResponse(request.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
#pragma warning restore CA1822

    private sealed record SerializerRequest(string PostalCode);

    private sealed record SerializerResponse(string ForecastText);
}

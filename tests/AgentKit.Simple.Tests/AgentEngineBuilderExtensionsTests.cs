// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

using AgentKit.FileSystem.InMemory;
using AgentKit.Permissions;
using AgentKit.Permissions.InMemory;
using AgentKit.Providers.OpenAI;
using AgentKit.Session.InMemory;
using AgentKit.Tools.Read;

/// <summary>Verifies AgentEngineBuilderExtensions behavior and contracts.</summary>
public sealed class AgentEngineBuilderExtensionsTests
{
    [Fact]
    public void UseLocalDevelopmentDefaults_WhenBuilderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((AgentEngineBuilder) null!).UseLocalDevelopmentDefaults()).ParamName.ShouldBe("builder");

    [Theory]
    [InlineData(null, "gpt-4o-mini", "apiKey")]
    [InlineData(" ", "gpt-4o-mini", "apiKey")]
    [InlineData("sk-test", null, "modelId")]
    [InlineData("sk-test", "", "modelId")]
    public void UseOpenAI_WhenAnArgumentIsBlank_ThrowsArgumentExceptionBeforeRegisteringTheProvider(string? apiKey, string? modelId, string parameter)
    {
        var builder = AgentEngine.CreateBuilder();
        var before = builder.Services.Count;

        Should.Throw<ArgumentException>(() => builder.UseOpenAI(apiKey!, modelId!)).ParamName.ShouldBe(parameter);
        builder.Services.Count.ShouldBe(before);
    }

    [Fact]
    public void UseOpenAI_WhenModelIsUnknown_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().UseOpenAI("sk-test", "gpt-imaginary")).ParamName.ShouldBe("modelId");

    [Fact]
    public void UseModel_WhenAliasIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => AgentEngine.CreateBuilder().UseModel(default)).ParamName.ShouldBe("alias");

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void WithInstructions_WhenTextIsBlank_ThrowsArgumentException(string? text) =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().WithInstructions(text!)).ParamName.ShouldBe("text");

    [Fact]
    public void WithIdentity_WhenNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => AgentEngine.CreateBuilder().WithIdentity(null!)).ParamName.ShouldBe("identity");

    [Fact]
    public void WithAgentId_WhenDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().WithAgentId(default)).ParamName.ShouldBe("agentId");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WithMaxTurns_WhenNotPositive_ThrowsArgumentOutOfRangeException(int maxTurns) =>
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().WithMaxTurns(maxTurns)).ParamName.ShouldBe("maxTurns");

    [Fact]
    public void WithAttemptTimeout_WhenZero_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().WithAttemptTimeout(TimeSpan.Zero)).ParamName.ShouldBe("timeout");

    [Fact]
    public void WithRequestSettings_WhenNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => AgentEngine.CreateBuilder().WithRequestSettings(null!)).ParamName.ShouldBe("settings");

    [Fact]
    public void Build_WhenNoModelWasSelected_FailsNamingTheFix()
    {
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults();

        var exception = Should.Throw<Exception>(builder.Build);

        Flatten(exception).ShouldContain("UseOpenAI");
        Flatten(exception).ShouldContain("UseModel");
    }

    [Fact]
    public void Build_WhenNoStorageOrSecurityWasRegistered_FailsWithTheEngineCompositionDiagnostic()
    {
        // Without the local defaults the engine's own validator speaks first, naming the first missing required service.
        var builder = AgentEngine.CreateBuilder().UseOpenAI("sk-test", "gpt-4o-mini");

        var exception = Should.Throw<Exception>(builder.Build);

        Flatten(exception).ShouldContain("agentkit.security-grant-store.missing");
    }

    [Fact]
    public void Build_WhenStorageAndSecurityAreSuppliedButNoIdentity_FailsNamingTheFix()
    {
        var builder = AgentEngine.CreateBuilder().UseOpenAI("sk-test", "gpt-4o-mini");
        _ = builder.Services.AddInMemorySecurityGrantStore().AddAllowAllSecurityPolicy();
        _ = builder.Services.AddInMemorySessionStore().AddInMemorySessionDirectory(new ComponentId("t"));

        var exception = Should.Throw<Exception>(builder.Build);

        Flatten(exception).ShouldContain("WithIdentity");
        Flatten(exception).ShouldContain("UseLocalDevelopmentDefaults");
    }

    [Fact]
    public async Task Build_WhenLocalDefaultsAndOpenAI_ProducesAnEngineHostingOneAgentWithoutTouchingTheNetwork()
    {
        await using var engine = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .Build();

        var agents = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);

        var definition = agents.ShouldHaveSingleItem();
        definition.Models.Candidates.ShouldBe([new ModelAlias("assistant")]);
        _ = engine.Conversation.ShouldBeAssignableTo<IConversationSession>();
    }

    [Fact]
    public async Task Build_WhenCallsAreChainedInAnyOrder_ReadsTheFinishedPlan()
    {
        var handler = new StubOpenAIHandler("ok");
        var builder = AgentEngine.CreateBuilder()
            .WithInstructions("First rule.")
            .WithMaxTurns(3)
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithInstructions("Second rule.")
            .UseLocalDevelopmentDefaults()
            .WithRequestSettings(LlmRequestSettings.Default with { Temperature = 0.2 });
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        _ = await engine.AskAsync("hello", TestContext.Current.CancellationToken);
        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Single();

        var body = handler.Bodies.Single();
        body.IndexOf("First rule.", StringComparison.Ordinal).ShouldBeLessThan(body.IndexOf("Second rule.", StringComparison.Ordinal));
        body.ShouldContain("\"temperature\":0.2");
        handler.Requests.Single().Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("sk-test");
        definition.Instructions.Length.ShouldBe(2);
        definition.RunDefaults.MaxTurns.ShouldBe(3);
    }

    [Fact]
    public async Task Build_WhenServicesRegisterATool_AdvertisesItToTheModelAndInTheDefinition()
    {
        var handler = new StubOpenAIHandler("done");
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini");
        _ = builder.Services.AddInMemoryFileSystem();
        _ = builder.Services.AddReadTool();
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        _ = await engine.AskAsync("hi", TestContext.Current.CancellationToken);
        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Single();

        handler.Bodies.Single().ShouldContain("\"name\":\"read_file\"");
        definition.Tools.ShouldHaveSingleItem().Name.ShouldBe("read_file");
    }

    [Fact]
    public async Task Build_WhenAnotherProviderIsRegisteredOnServices_UseModelSelectsIt()
    {
        var handler = new StubOpenAIHandler("via alias");
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults();
        _ = builder.Services.AddOpenAI();
        _ = builder.Services.AddOpenAIApiKeyCredential("sk-test");
        _ = builder.Services.AddOpenAIKnownLlmModel(new ModelAlias("custom"), new ModelId("gpt-4o"));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.UseModel(new ModelAlias("custom")).Build();

        var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

        reply.ShouldBe("via alias");
        handler.Bodies.Single().ShouldContain("\"model\":\"gpt-4o\"");
    }

    [Fact]
    public async Task Build_WhenAgentIdIsPinned_PublishesTheDefinitionUnderIt()
    {
        var agentId = new AgentId(Guid.Parse("12345678-1234-1234-1234-123456789012"));
        await using var engine = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithAgentId(agentId)
            .Build();

        (await engine.GetAgentAsync(agentId, TestContext.Current.CancellationToken)).ShouldNotBeNull().Definition.Id.ShouldBe(agentId);
    }

    private static string Flatten(Exception exception) =>
        string.Join(" | ", Enumerate(exception).Select(static e => e.Message));

    private static IEnumerable<Exception> Enumerate(Exception exception)
    {
        yield return exception;
        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions.SelectMany(Enumerate))
            {
                yield return inner;
            }
        }
        else if (exception.InnerException is { } single)
        {
            foreach (var inner in Enumerate(single))
            {
                yield return inner;
            }
        }
    }
}

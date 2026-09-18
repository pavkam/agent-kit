// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

using AgentKit.FileSystem.InMemory;
using AgentKit.Permissions;
using AgentKit.Permissions.InMemory;
using AgentKit.Providers.Anthropic;
using AgentKit.Providers.AzureOpenAI;
using AgentKit.Providers.Ollama;
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

    [Theory]
    [InlineData(null, "claude-sonnet-4-5", "apiKey")]
    [InlineData(" ", "claude-sonnet-4-5", "apiKey")]
    [InlineData("sk-ant", null, "modelId")]
    [InlineData("sk-ant", "", "modelId")]
    public void UseAnthropic_WhenAnArgumentIsBlank_ThrowsArgumentExceptionBeforeRegisteringTheProvider(string? apiKey, string? modelId, string parameter)
    {
        var builder = AgentEngine.CreateBuilder();
        var before = builder.Services.Count;

        Should.Throw<ArgumentException>(() => builder.UseAnthropic(apiKey!, modelId!)).ParamName.ShouldBe(parameter);
        builder.Services.Count.ShouldBe(before);
    }

    [Fact]
    public void UseAnthropic_WhenModelIsUnknown_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().UseAnthropic("sk-ant", "claude-imaginary")).ParamName.ShouldBe("modelId");

    [Fact]
    public async Task UseAnthropic_WhenBuilt_SendsToAnthropicWithTheKeyHeaderAndPublishesTheKnownDescriptor()
    {
        var handler = new ThrowingHandler();
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseAnthropic("sk-ant-test", "claude-sonnet-4-5");
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        _ = await Should.ThrowAsync<SimpleAgentException>(() => engine.AskAsync("hello", TestContext.Current.CancellationToken));
        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Single();
        var snapshot = await engine.Services.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.RequestUri.ShouldNotBeNull().Host.ShouldBe("api.anthropic.com");
        request.Headers.GetValues("x-api-key").ShouldBe(["sk-ant-test"]);
        definition.Models.Candidates.ShouldBe([new ModelAlias("assistant")]);
        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.ProviderId.ShouldBe(AnthropicProviderDefaults.ProviderId);
        published.ModelId.ShouldBe(new ModelId("claude-sonnet-4-5"));
        _ = published.Pricing.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(null, "modelId")]
    [InlineData("  ", "modelId")]
    public void UseOllama_WhenModelIdIsBlank_ThrowsArgumentExceptionBeforeRegisteringTheProvider(string? modelId, string parameter)
    {
        var builder = AgentEngine.CreateBuilder();
        var before = builder.Services.Count;

        Should.Throw<ArgumentException>(() => builder.UseOllama(modelId!)).ParamName.ShouldBe(parameter);
        builder.Services.Count.ShouldBe(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UseOllama_WhenApiKeyIsSuppliedButBlank_ThrowsArgumentException(string apiKey) =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().UseOllama("llama3.1:8b", apiKey)).ParamName.ShouldBe("apiKey");

    [Fact]
    public async Task UseOllama_WhenBuilt_SendsToTheConfiguredServerWithThePlaceholderTokenAndCompletesATurn()
    {
        var handler = new StubOpenAIHandler("local reply");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOllama("llama3.1:8b", configure: o => o.BaseAddress = new Uri("http://127.0.0.1:11434/v1/"));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);
        var snapshot = await engine.Services.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);

        reply.ShouldBe("local reply");
        var request = handler.Requests.ShouldHaveSingleItem();
        request.RequestUri.ShouldNotBeNull().GetLeftPart(UriPartial.Authority).ShouldBe("http://127.0.0.1:11434");
        request.Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("ollama");
        handler.Bodies.Single().ShouldContain("\"model\":\"llama3.1:8b\"");
        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.ProviderId.ShouldBe(OllamaProviderDefaults.ProviderId);
        published.Pricing.ShouldBeNull();
    }

    [Fact]
    public async Task UseOllama_WhenAKeyIsSupplied_SendsThatKey()
    {
        var handler = new StubOpenAIHandler("ok");
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOllama("llama3.1:8b", "remote-key");
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        _ = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

        handler.Requests.Single().Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("remote-key");
    }

    [Theory]
    [InlineData(null, "openai/gpt-4o-mini", "apiKey")]
    [InlineData(" ", "openai/gpt-4o-mini", "apiKey")]
    [InlineData("sk-or", null, "modelId")]
    [InlineData("sk-or", "", "modelId")]
    public void UseOpenRouter_WhenAnArgumentIsBlank_ThrowsArgumentExceptionBeforeRegisteringTheProvider(string? apiKey, string? modelId, string parameter)
    {
        var builder = AgentEngine.CreateBuilder();
        var before = builder.Services.Count;

        Should.Throw<ArgumentException>(() => builder.UseOpenRouter(apiKey!, modelId!)).ParamName.ShouldBe(parameter);
        builder.Services.Count.ShouldBe(before);
    }

    [Fact]
    public async Task UseOpenRouter_WhenBuilt_SendsToOpenRouterWithTheBearerKeyAndCompletesATurn()
    {
        var handler = new StubOpenAIHandler("routed");
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenRouter("sk-or-test", "openai/gpt-4o-mini");
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

        reply.ShouldBe("routed");
        var request = handler.Requests.ShouldHaveSingleItem();
        request.RequestUri.ShouldNotBeNull().Host.ShouldBe("openrouter.ai");
        request.Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("sk-or-test");
        handler.Bodies.Single().ShouldContain("\"model\":\"openai/gpt-4o-mini\"");
    }

    [Fact]
    public void UseAzureOpenAI_WhenEndpointIsNull_ThrowsArgumentNullExceptionBeforeRegisteringTheProvider()
    {
        var builder = AgentEngine.CreateBuilder();
        var before = builder.Services.Count;

        Should.Throw<ArgumentNullException>(() => builder.UseAzureOpenAI(null!, "key", "dep", "gpt-4o-mini")).ParamName.ShouldBe("resourceEndpoint");
        builder.Services.Count.ShouldBe(before);
    }

    [Theory]
    [InlineData(null, "dep", "gpt-4o-mini", "apiKey")]
    [InlineData("key", " ", "gpt-4o-mini", "deploymentId")]
    [InlineData("key", "dep", "", "modelId")]
    public void UseAzureOpenAI_WhenAStringArgumentIsBlank_ThrowsArgumentExceptionBeforeRegisteringTheProvider(string? apiKey, string? deploymentId, string? modelId, string parameter)
    {
        var builder = AgentEngine.CreateBuilder();
        var before = builder.Services.Count;

        Should.Throw<ArgumentException>(() => builder.UseAzureOpenAI(new Uri("https://acme.openai.azure.com/"), apiKey!, deploymentId!, modelId!)).ParamName.ShouldBe(parameter);
        builder.Services.Count.ShouldBe(before);
    }

    [Fact]
    public async Task UseAzureOpenAI_WhenTheModelIsKnown_OverlaysTheOpenAIFactsOntoTheDeploymentDescriptor()
    {
        var handler = new StubOpenAIHandler("azure reply");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseAzureOpenAI(new Uri("https://acme.openai.azure.com/"), "azure-key", "chat-deployment", "gpt-4o-mini");
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);
        var snapshot = await engine.Services.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);

        reply.ShouldBe("azure reply");
        handler.Requests.Single().RequestUri.ShouldNotBeNull().Host.ShouldBe("acme.openai.azure.com");
        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.ProviderId.ShouldBe(AzureOpenAIProviderDefaults.ProviderId);
        published.ApiFamily.ShouldBe(AzureOpenAIProviderDefaults.ApiFamily);
        published.DeploymentId.ShouldBe(new DeploymentId("chat-deployment"));
        published.Limits.MaxContextTokens.ShouldBe(128000);
        _ = published.Pricing.ShouldNotBeNull();
    }

    [Fact]
    public async Task UseAzureOpenAI_WhenTheModelIsUnknown_UsesTheAdapterDefaults()
    {
        await using var engine = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseAzureOpenAI(new Uri("https://acme.openai.azure.com/"), "azure-key", "custom-deployment", "my-finetune")
            .Build();

        var snapshot = await engine.Services.GetRequiredService<IModelCatalog>().GetSnapshotAsync(TestContext.Current.CancellationToken);

        var published = snapshot.ConversationModels.ShouldHaveSingleItem();
        published.ModelId.ShouldBe(new ModelId("my-finetune"));
        published.DeploymentId.ShouldBe(new DeploymentId("custom-deployment"));
        published.Pricing.ShouldBeNull();
        published.Limits.ShouldBe(AzureOpenAIProviderDefaults.DefaultLimits);
    }

    [Fact]
    public void UseOpenRouter_WhenAnotherSugarMethodAlreadySelectedAModel_ThrowsInvalidOperationExceptionNamingIt()
    {
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini");
        var before = builder.Services.Count;

        var exception = Should.Throw<InvalidOperationException>(() => builder.UseOpenRouter("sk-or", "openai/gpt-4o-mini"));

        exception.Message.ShouldContain("UseOpenAI");
        exception.Message.ShouldContain("UseModel");
        builder.Services.Count.ShouldBe(before);
    }

    [Fact]
    public void WithOutput_WhenDefinitionIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => AgentEngine.CreateBuilder().WithOutput(null!)).ParamName.ShouldBe("definition");

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void WithOutputOfT_WhenSchemaIsBlank_ThrowsArgumentException(string? schema) =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().WithOutput<string>(schema!)).ParamName.ShouldBe("schemaJson");

    [Fact]
    public void WithOutputOfT_WhenSchemaIsNotAnObject_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().WithOutput<string>("[1,2]")).ParamName.ShouldBe("schemaJson");

    [Fact]
    public void WithOutputOfT_WhenSchemaIsNotJson_ThrowsJsonException() =>
        _ = Should.Throw<System.Text.Json.JsonException>(() => AgentEngine.CreateBuilder().WithOutput<string>("{not json"));

    [Fact]
    public void WithOutputOfT_WhenNameIsWhitespace_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().WithOutput<string>("{}", name: " ")).ParamName.ShouldBe("name");

    [Fact]
    public void WithOutputOfT_WhenRepairAttemptsAreNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().WithOutput<string>("{}", maximumRepairAttempts: -1)).ParamName.ShouldBe("maximumRepairAttempts");

    [Fact]
    public async Task WithOutputOfT_WhenBuilt_PublishesTheDefinitionOnTheAgentAndAnInstructionCarryingTheSchema()
    {
        await using var engine = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithOutput<int>("""{"type":"object","properties":{"n":{"type":"integer"}}}""", name: "count", maximumRepairAttempts: 3)
            .Build();

        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Single();

        var output = definition.Output.ShouldNotBeNull();
        output.Name.ShouldBe("count");
        output.Mode.ShouldBe(OutputMode.Prompted);
        output.RuntimeType.ShouldBe(typeof(int));
        output.RetryPolicy.MaximumAttempts.ShouldBe(3);
        output.Schema.ShouldNotBeNull().Schema.GetProperty("properties").GetProperty("n").GetProperty("type").GetString().ShouldBe("integer");
        definition.Instructions.OfType<SystemMessage>().Select(static m => ((TextPart) m.Parts[0]).Text)
            .ShouldContain(text => text.Contains("JSON Schema", StringComparison.Ordinal) && text.Contains("\"n\"", StringComparison.Ordinal));
    }

    [Fact]
    public void AddAgent_WhenConfigureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => AgentEngine.CreateBuilder().AddAgent(new AgentId(Guid.NewGuid()), null!)).ParamName.ShouldBe("configure");

    [Fact]
    public void AddAgent_WhenAgentIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().AddAgent(default, static _ => { })).ParamName.ShouldBe("agentId");

    [Fact]
    public void AddAgent_WhenTheConfiguredTurnLimitIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().AddAgent(new AgentId(Guid.NewGuid()), static o => o.MaxTurns = 0)).ParamName.ShouldBe("configure");

    [Fact]
    public void AddAgent_WhenTheConfiguredDisplayNameIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().AddAgent(new AgentId(Guid.NewGuid()), static o => o.DisplayName = " ")).ParamName.ShouldBe("configure");

    [Fact]
    public void AddAgent_WhenTheIdentityIsAlreadyHosted_ThrowsInvalidOperationException()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var builder = AgentEngine.CreateBuilder().AddAgent(agentId, static _ => { });

        _ = Should.Throw<InvalidOperationException>(() => builder.AddAgent(agentId, static _ => { }));
        _ = Should.Throw<InvalidOperationException>(() => AgentEngine.CreateBuilder().WithAgentId(agentId).AddAgent(agentId, static _ => { }));
    }

    [Fact]
    public async Task AddAgent_WhenBuilt_HostsBothAgentsAndDrivesEachThroughItsOwnSessions()
    {
        var handler = new StubOpenAIHandler("reply");
        var reviewer = new AgentId(Guid.Parse("7a000000-0000-0000-0000-000000000002"));
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithInstructions("You are the default agent.")
            .AddAgent(reviewer, o =>
            {
                o.DisplayName = "reviewer";
                o.Instructions.Add("You are the reviewer.");
                o.MaxTurns = 3;
                o.IncludeRegisteredTools = false;
            });
        _ = builder.Services.AddInMemoryFileSystem();
        _ = builder.Services.AddReadTool();
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var agents = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);
        var reviewerAgent = (await engine.GetAgentAsync(reviewer, TestContext.Current.CancellationToken))!;
        var defaultAgent = (await engine.GetAgentAsync(agents.Single(a => a.Id != reviewer).Id, TestContext.Current.CancellationToken))!;

        var first = await reviewerAgent.SendAsync(new AgentSendRequest(engine.Identity, "review this"), TestContext.Current.CancellationToken);
        var second = await reviewerAgent.SendAsync(new AgentSendRequest(engine.Identity, "and this", first.SessionId), TestContext.Current.CancellationToken);
        var other = await defaultAgent.SendAsync(new AgentSendRequest(engine.Identity, "hello"), TestContext.Current.CancellationToken);
        var viaConversation = await engine.AskAsync("hello again", TestContext.Current.CancellationToken);

        agents.Length.ShouldBe(2);
        reviewerAgent.Definition.DisplayName.ShouldBe("reviewer");
        reviewerAgent.Definition.RunDefaults.MaxTurns.ShouldBe(3);
        reviewerAgent.Definition.Tools.ShouldBeEmpty();
        defaultAgent.Definition.Tools.ShouldHaveSingleItem().Name.ShouldBe("read_file");
        _ = first.Outcome.ShouldBeOfType<AgentRunCompleted>();
        second.SessionId.ShouldBe(first.SessionId);
        other.SessionId.ShouldNotBe(first.SessionId);
        viaConversation.ShouldBe("reply");
        handler.Bodies.Count.ShouldBe(4);
        handler.Bodies[0].ShouldContain("You are the reviewer.");
        handler.Bodies[0].ShouldNotContain("default agent");
        handler.Bodies[0].ShouldNotContain("read_file");
        handler.Bodies[1].ShouldContain("review this");
        handler.Bodies[1].ShouldContain("and this");
        handler.Bodies[2].ShouldContain("You are the default agent.");
        handler.Bodies[2].ShouldContain("read_file");
    }

    [Fact]
    public async Task AddAgent_WhenTurnsTargetDifferentSessions_RunConcurrentlyOnOneEngine()
    {
        var handler = new StubOpenAIHandler("reply");
        var worker = new AgentId(Guid.Parse("7a000000-0000-0000-0000-000000000003"));
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .AddAgent(worker, static o => o.Instructions.Add("Work."));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(worker, TestContext.Current.CancellationToken))!;

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(i => agent.SendAsync(new AgentSendRequest(engine.Identity, $"job {i}"), TestContext.Current.CancellationToken)));

        results.Select(static r => r.SessionId).Distinct().Count().ShouldBe(4);
        results.ShouldAllBe(static r => r.Outcome is AgentRunCompleted);
        handler.Bodies.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Identity_WhenBuiltWithLocalDefaults_ReturnsTheProcessUserIdentityEveryTurnUses()
    {
        await using var engine = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini").Build();

        var identity = engine.Identity;

        identity.TenantId.ShouldBe(new TenantId("local"));
        identity.SubjectKind.ShouldBe(ExecutionSubjectKind.Human);
        engine.Identity.ShouldBe(identity);
    }

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
    public void WithIdentity_WhenValid_SetsThePlanIdentity()
    {
        var builder = AgentEngine.CreateBuilder();
        var identity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

        _ = builder.WithIdentity(identity);

        AgentEngineBuilderExtensions.Plan(builder).Identity.ShouldBeSameAs(identity);
    }

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
    public void WithAttemptTimeout_WhenPositive_SetsThePlanAttemptTimeout()
    {
        var builder = AgentEngine.CreateBuilder();

        _ = builder.WithAttemptTimeout(TimeSpan.FromSeconds(42));

        AgentEngineBuilderExtensions.Plan(builder).AttemptTimeout.ShouldBe(TimeSpan.FromSeconds(42));
    }

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

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("relative/sessions.db")]
    public void UseSqliteSessions_WhenPathIsBlankOrRelative_ThrowsArgumentException(string? path) =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().UseSqliteSessions(path!)).ParamName.ShouldBe("databasePath");

    [Fact]
    public void UseSqliteSessions_WhenTheDirectoryCannotBeCreated_ThrowsInvalidOperationExceptionAttributedToThisCall()
    {
        // An ancestor path segment that is actually a file makes Directory.CreateDirectory fail with a
        // raw IOException; UseSqliteSessions must translate that into an exception that clearly names
        // this call as the cause, not an unrelated filesystem failure.
        using var directory = new TempDirectory();
        var blockingFile = Path.Combine(directory.Path, "not-a-directory");
        File.WriteAllText(blockingFile, "blocking");
        var databasePath = Path.Combine(blockingFile, "nested", "sessions.db");

        var exception = Should.Throw<InvalidOperationException>(
            () => AgentEngine.CreateBuilder().UseSqliteSessions(databasePath));

        exception.Message.ShouldContain("could not be created");
        _ = exception.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public async Task UseSqliteSessions_WhenTheProcessRestarts_ResumesTheConversationFromDisk()
    {
        using var directory = new TempDirectory();
        var databasePath = Path.Combine(directory.Path, "sessions.db");
        SessionId sessionId;

        await using (var first = SqliteEngine(databasePath, new StubOpenAIHandler("remembered answer")))
        {
            _ = await first.AskAsync("remember this", TestContext.Current.CancellationToken);
            var listed = (await first.Conversation.ListAsync(null, 10, TestContext.Current.CancellationToken)).ShouldBeOfType<ConversationSessionPage>();
            sessionId = listed.Sessions.ShouldHaveSingleItem().SessionId;
        }

        await using var second = SqliteEngine(databasePath, new StubOpenAIHandler("unused"));
        var opened = await second.Conversation.OpenAsync(sessionId, TestContext.Current.CancellationToken);
        var history = await second.Conversation.ReadHistoryAsync(new SessionSequence(0), 50, TestContext.Current.CancellationToken);

        _ = opened.ShouldBeOfType<ConversationSessionOpened>();
        var messages = history.ShouldBeOfType<ConversationHistoryPage>().Messages;
        messages.OfType<UserMessage>().ShouldHaveSingleItem().Parts.OfType<TextPart>().Single().Text.ShouldBe("remember this");
        messages.OfType<AssistantMessage>().ShouldHaveSingleItem().Parts.OfType<TextPart>().Single().Text.ShouldBe("remembered answer");
    }

    [Fact]
    public async Task UseSqliteSessions_WhenCalledBeforeLocalDefaults_StillSelectsSqlite()
    {
        using var directory = new TempDirectory();
        var handler = new StubOpenAIHandler("ok");
        var builder = AgentEngine.CreateBuilder()
            .UseSqliteSessions(Path.Combine(directory.Path, "s.db"))
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini");
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        _ = await engine.AskAsync("hi", TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(directory.Path, "s.db")).ShouldBeTrue();
        builder.Services.Count(static d => d.ServiceType == typeof(ISessionDirectory)).ShouldBe(1);
        (await engine.Conversation.ListAsync(null, 10, TestContext.Current.CancellationToken))
            .ShouldBeOfType<ConversationSessionPage>().Sessions.ShouldHaveSingleItem().StoreKey.Value.ShouldBe("agentkit.sqlite");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/dir")]
    public void UseWorkspace_WhenRootIsBlankOrRelative_ThrowsArgumentException(string? root) =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().UseWorkspace(root!)).ParamName.ShouldBe("rootDirectory");

    [Fact]
    public async Task UseWorkspace_WhenTheModelReadsAFile_ReturnsItsContentThroughTheSandboxedTool()
    {
        using var directory = new TempDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "notes.txt"), "the answer is 42", TestContext.Current.CancellationToken);
        var handler = new StubOpenAIHandler("tool:read_file:{\"path\":\"notes.txt\"}", "It says 42.");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .UseWorkspace(directory.Path);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var result = await engine.SendAsync("what do my notes say?", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Events.OfType<ConversationToolResultEvent>().ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
        handler.Bodies[1].ShouldContain("the answer is 42");
        foreach (var tool in new[] { "read_file", "write_file", "edit", "glob", "search", "list_directory" })
        {
            handler.Bodies[0].ShouldContain($"\"name\":\"{tool}\"", customMessage: $"tool {tool} was not advertised");
        }
    }

    [Fact]
    public async Task UseWorkspace_WhenTheModelReadsOutsideTheRoot_TheSandboxRefuses()
    {
        using var directory = new TempDirectory();
        var handler = new StubOpenAIHandler("tool:read_file:{\"path\":\"../../etc/passwd\"}", "could not");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .UseWorkspace(directory.Path);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var result = await engine.SendAsync("read the password file", TestContext.Current.CancellationToken);

        result.Events.OfType<ConversationToolResultEvent>().ShouldHaveSingleItem().Succeeded.ShouldBeFalse();
        handler.Bodies[1].ShouldNotContain("root:");
    }

    [Fact]
    public async Task Build_WhenAnAdditionalPolicyDeniesWrites_DenyWinsOverTheLocalAllowAllPolicy()
    {
        // The permissions guide's read-only policy: deny overrides allow, so the write is refused before any effect.
        using var directory = new TempDirectory();
        var handler = new StubOpenAIHandler("tool:write_file:{\"path\":\"out.txt\",\"content\":\"x\",\"mode\":\"create_only\"}", "denied, ok");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .UseWorkspace(directory.Path);
        _ = builder.Services.AddSingleton<ISecurityPolicy, ReadOnlyWorkspacePolicy>();
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        await using var engine = builder.Build();

        var result = await engine.SendAsync("write a file", TestContext.Current.CancellationToken);

        result.Events.OfType<ConversationToolResultEvent>().ShouldHaveSingleItem().Succeeded.ShouldBeFalse();
        File.Exists(Path.Combine(directory.Path, "out.txt")).ShouldBeFalse();
    }

    /// <summary>The policy shown in docs/guides/permissions.md.</summary>
    private sealed class ReadOnlyWorkspacePolicy: ISecurityPolicy
    {
        public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var mutates = request.Kind is SecurityOperationKind.FileWrite or SecurityOperationKind.DirectoryCreate or SecurityOperationKind.Process;
            return ValueTask.FromResult(mutates
                ? new SecurityPolicyResult(SecurityPolicyResultKind.Deny, "read-only", "This agent may only read the workspace.")
                : new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null));
        }
    }

    private static AgentEngine SqliteEngine(string databasePath, HttpMessageHandler handler)
    {
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseSqliteSessions(databasePath)
            .UseOpenAI("sk-test", "gpt-4o-mini");
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        return builder.Build();
    }

    private sealed class TempDirectory: IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"agentkit-simple-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
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

    private sealed class ThrowingHandler: HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            throw new HttpRequestException("connection refused");
        }
    }
}

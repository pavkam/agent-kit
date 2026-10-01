// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

using AgentKit.Context.Compaction;
using AgentKit.Durability.InMemory;
using AgentKit.FileSystem.InMemory;
using AgentKit.Goals.Hosting;
using AgentKit.Hooks;
using AgentKit.Loop;
using AgentKit.Memory;
using AgentKit.Network;
using AgentKit.Network.InMemory;
using AgentKit.Permissions;
using AgentKit.Permissions.InMemory;
using AgentKit.Providers.Anthropic;
using AgentKit.Providers.AzureOpenAI;
using AgentKit.Providers.Ollama;
using AgentKit.Providers.OpenAI;
using AgentKit.Session.InMemory;
using AgentKit.TestSupport;
using AgentKit.Tools;
using AgentKit.Tools.Read;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        _ = await Should.ThrowAsync<SimpleAgentException>(() => engine.AskAsync("hello", TestContext.Current.CancellationToken));
        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions.Single();
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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
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
        _ = Should.Throw<JsonException>(() => AgentEngine.CreateBuilder().WithOutput<string>("{not json"));

    [Fact]
    public void WithOutputOfT_WhenNameIsWhitespace_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().WithOutput<string>("{}", name: " ")).ParamName.ShouldBe("name");

    [Fact]
    public void WithOutputOfT_WhenRepairAttemptsAreNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().WithOutput<string>("{}", maximumRepairAttempts: -1)).ParamName.ShouldBe("maximumRepairAttempts");

    [Fact]
    public async Task WithOutputOfT_WhenModeIsNativeSchema_PublishesNativeSchemaWithoutPromptedInstruction()
    {
        await using var engine = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithOutput<int>(/*lang=json,strict*/ """{"type":"object","properties":{"n":{"type":"integer"}}}""", mode: OutputMode.NativeSchema)
            .Build();

        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions.Single().Output.ShouldNotBeNull();
        definition.Mode.ShouldBe(OutputMode.NativeSchema);
        (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions.Single().Instructions
            .OfType<SystemMessage>()
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task WithOutputOfT_WhenBuilt_PublishesTheDefinitionOnTheAgentAndAnInstructionCarryingTheSchema()
    {
        await using var engine = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithOutput<int>(/*lang=json,strict*/ """{"type":"object","properties":{"n":{"type":"integer"}}}""", name: "count", maximumRepairAttempts: 3)
            .Build();

        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions.Single();

        var output = definition.Output.ShouldNotBeNull();
        output.Name.ShouldBe("count");
        output.Mode.ShouldBe(OutputMode.Prompted);
        output.RuntimeType.ShouldBe(typeof(int));
        output.RetryPolicy.MaximumAttempts.ShouldBe(3);
        output.Schema.ShouldNotBeNull().Schema.GetProperty("properties").GetProperty("n").GetProperty("type").GetString().ShouldBe("integer");
        InstructionSourceProjection.ToMessages(definition.Instructions).OfType<SystemMessage>().Select(static m => ((TextPart) m.Parts[0]).Text)
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
        RegisterInMemoryReadTool(builder);
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        var agents = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions;
        var reviewerAgent = (await engine.GetAgentAsync(reviewer, TestContext.Current.CancellationToken)).ShouldBeOfType<ResolvedAgent>().Agent;
        var defaultAgent = (await engine.GetAgentAsync(agents.Single(a => a.Id != reviewer).Id, TestContext.Current.CancellationToken)).ShouldBeOfType<ResolvedAgent>().Agent;

        var first = await reviewerAgent.SendAsync(new AgentSendRequest(engine.Identity, "review this"), TestContext.Current.CancellationToken);
        var second = await reviewerAgent.SendAsync(new AgentSendRequest(engine.Identity, "and this", first.SessionId), TestContext.Current.CancellationToken);
        var other = await defaultAgent.SendAsync(new AgentSendRequest(engine.Identity, "hello"), TestContext.Current.CancellationToken);
        var viaConversation = await engine.AskAsync("hello again", TestContext.Current.CancellationToken);

        agents.Length.ShouldBe(2);
        reviewerAgent.Definition.DisplayName.ShouldBe("reviewer");
        reviewerAgent.Definition.RunDefaults.MaxTurns.ShouldBe(3);
        reviewerAgent.Definition.Toolsets.ShouldBeEmpty();
        defaultAgent.Definition.Toolsets.ShouldNotBeEmpty();
        _ = first.Outcome.ShouldBeOfType<RunSucceeded>();
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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();
        var agent = (await engine.GetAgentAsync(worker, TestContext.Current.CancellationToken)).ShouldBeOfType<ResolvedAgent>().Agent;

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(i => agent.SendAsync(new AgentSendRequest(engine.Identity, $"job {i}"), TestContext.Current.CancellationToken)));

        results.Select(static r => r.SessionId).Distinct().Count().ShouldBe(4);
        results.ShouldAllBe(static r => r.Outcome is RunSucceeded);
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
        var identity = TestExecutionIdentity.Create(
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
        _ = builder.Services.AddAgentPermissions(static o =>
        {
            o.PolicySnapshot = TestSecurityEvidence.PolicySnapshot;
            o.AuditDelivery = SecurityAuditDelivery.BestEffort;
        });
        _ = builder.Services.AddInMemorySecurityGrantStore();
        _ = builder.Services.AddInMemoryApprovalStore();
        _ = builder.Services.AddInMemorySecurityDecisionStore();
        _ = builder.Services.AddAllowAllSecurityPolicy();
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

        var agents = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions;

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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        _ = await engine.AskAsync("hello", TestContext.Current.CancellationToken);
        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions.Single();

        var body = handler.Bodies.Single();
        body.IndexOf("First rule.", StringComparison.Ordinal).ShouldBeLessThan(body.IndexOf("Second rule.", StringComparison.Ordinal));
        body.ShouldContain("\"temperature\":0.2");
        handler.Requests.Single().Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("sk-test");
        InstructionSourceProjection.ToMessages(definition.Instructions).Length.ShouldBe(2);
        definition.RunDefaults.MaxTurns.ShouldBe(3);
    }

    [Fact]
    public async Task Build_WhenServicesRegisterATool_AdvertisesItToTheModelAndInTheDefinition()
    {
        var handler = new StubOpenAIHandler("done");
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini");
        RegisterInMemoryReadTool(builder);
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        _ = await engine.AskAsync("hi", TestContext.Current.CancellationToken);
        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions.Single();

        handler.Bodies.Single().ShouldContain("\"name\":\"read_file\"");
        definition.Toolsets.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Build_WhenABeforeToolInvocationHookVetoesACall_TheModelSeesARejectedResultAndTheToolNeverRuns()
    {
        var handler = new StubOpenAIHandler("tool:read_file:{\"path\":\"secret.txt\"}", "understood");
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini");
        RegisterInMemoryReadTool(builder);
        _ = builder.Services.AddBeforeToolInvocationHook<VetoSecretsHook>(
            HookRegistrationDescriptors.ForPoint(
                new HookId("test.veto-secrets"),
                AgentHookPointDefinitions.BeforeToolInvocationRegistration));
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        var result = await engine.SendAsync("read the secret", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        var toolResult = result.Events.OfType<ConversationToolResultEvent>().ShouldHaveSingleItem();
        toolResult.Succeeded.ShouldBeFalse();
        handler.Bodies.Count.ShouldBe(2);
        handler.Bodies[1].ShouldContain("secrets stay secret");
    }

    [Fact]
    public void WithCompaction_WhenBuilderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((AgentEngineBuilder) null!).WithCompaction()).ParamName.ShouldBe("builder");

    [Fact]
    public async Task WithCompaction_WhenBuilt_RegistersACompactorAndTurnsStillComplete()
    {
        var handler = new StubOpenAIHandler("fine");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithCompaction(o => o.MaximumCheckpointCharacters = 8_000);
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

        reply.ShouldBe("fine");
        _ = engine.Services.GetRequiredService<ICompactor>().ShouldNotBeNull();
        engine.Services.GetRequiredService<IOptions<CompactionOptions>>().Value.MaximumCheckpointCharacters.ShouldBe(8_000);
    }

    [Fact]
    public async Task WithCompaction_WhenBuilt_SelectsAnExtractiveProfileBoundToTheDefaultCompactorOnEveryDefinition()
    {
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini").WithCompaction();
        await using var engine = builder.Build();

        var snapshot = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);
        var profileKey = snapshot.Definitions.Single().OptionalCapabilities.CompactionProfile.ShouldNotBeNull();
        engine.Services.GetRequiredService<ICompactionProfileCatalog>().TryGet(profileKey, out var publication).ShouldBeTrue();

        publication.ShouldNotBeNull().Enabled.ShouldBeTrue();
        publication.CompactorKey.ShouldBe(AgentContextCompactionComponentDefaults.CompactorKey);
        publication.Policy.StrategyOrder.ShouldBe([CompactionStrategyKeys.Extractive]);
        engine.Services.GetRequiredKeyedService<ICompactor>(publication.CompactorKey.Value)
            .ShouldBeSameAs(engine.Services.GetRequiredService<ICompactor>());
    }

    [Fact]
    public async Task WithCompaction_WhenCalledTwice_BuildsWithASingleProfile()
    {
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini")
            .WithCompaction(o => o.MaximumCheckpointCharacters = 8_000)
            .WithCompaction(o => o.MaximumSourceEntries = 100);
        await using var engine = builder.Build();

        var options = engine.Services.GetRequiredService<IOptions<CompactionOptions>>().Value;
        options.MaximumCheckpointCharacters.ShouldBe(8_000);
        options.MaximumSourceEntries.ShouldBe(100);
        builder.Services.Count(static d => d.ServiceType == typeof(ICompactionProfileCatalog)).ShouldBe(1);
    }

    [Fact]
    public async Task WithCompaction_WhenAPromptExceedsTheModelWindow_TheLoopCompactsUnderTheProfilePolicy()
    {
        var handler = new StubOpenAIHandler("fine");
        var log = new CapturedCompactionRequests();
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithCompaction();
        _ = builder.Services.AddSingleton(log);
        _ = builder.Services.ReplaceCompactor<CapturingCompactor>(AgentContextCompactionComponentDefaults.CompactorKey);
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        // gpt-4o-mini declares a 128,000-token window; the loop estimates four characters per token and compacts past 80%.
        var reply = await engine.AskAsync(new string('x', 4 * 110_000), TestContext.Current.CancellationToken);

        reply.ShouldBe("fine");
        var policy = log.Requests.ShouldHaveSingleItem().Policy.ShouldNotBeNull();
        var snapshot = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);
        policy.ProfileKey.ShouldBe(snapshot.Definitions.Single().OptionalCapabilities.CompactionProfile.ShouldNotBeNull());
        policy.CompactorKey.ShouldBe(AgentContextCompactionComponentDefaults.CompactorKey);
        policy.StrategyOrder.ShouldBe([CompactionStrategyKeys.Extractive]);
    }

    [Fact]
    public async Task AgentCompactAsync_WhenTheAgentSelectsACompactionProfile_RunsMaintenanceThroughTheProfilesCompactorWithItsPolicy()
    {
        var log = new CapturedCompactionRequests();
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini").WithCompaction();
        _ = builder.Services.AddSingleton(log);
        _ = builder.Services.ReplaceCompactor<CapturingCompactor>(AgentContextCompactionComponentDefaults.CompactorKey);
        _ = builder.Services.ReplaceNetworkWithHandler(new StubOpenAIHandler("fine"));
        await using var engine = builder.Build();
        _ = await engine.AskAsync("hello", TestContext.Current.CancellationToken);
        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions.Single();
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).ShouldBeOfType<ResolvedAgent>().Agent;

        var result = await agent.CompactAsync(
            engine.Conversation.SessionId.ShouldNotBeNull(),
            engine.Conversation.BranchId.ShouldNotBeNull(),
            engine.Identity,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<CompactionNotReducing>();
        var request = log.Requests.ShouldHaveSingleItem();
        request.Trigger.Kind.ShouldBe(CompactionTriggerKind.ExplicitMaintenance);
        request.Policy.ShouldNotBeNull().ProfileKey.ShouldBe(definition.OptionalCapabilities.CompactionProfile.ShouldNotBeNull());
    }

    [Fact]
    public async Task AgentCompactAsync_WhenTheSelectedProfileDisablesCompaction_ReturnsADisabledRejectionWithoutCallingACompactor()
    {
        var log = new CapturedCompactionRequests();
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini").WithCompaction();
        _ = builder.Services.AddSingleton(log);
        _ = builder.Services.ReplaceCompactor<CapturingCompactor>(AgentContextCompactionComponentDefaults.CompactorKey);
        _ = builder.Services.ReplaceNetworkWithHandler(new StubOpenAIHandler("fine"));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton<ICompactionProfileCatalog>(new StaticCompactionProfileCatalog(
            CompactionPolicyFixtures.Publication(
                enabled: false,
                profileKey: new CompactionProfileKey("agentkit.simple.compaction"),
                compactorKey: AgentContextCompactionComponentDefaults.CompactorKey))));
        await using var engine = builder.Build();
        _ = await engine.AskAsync("hello", TestContext.Current.CancellationToken);
        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions.Single();
        var agent = (await engine.GetAgentAsync(definition.Id, TestContext.Current.CancellationToken)).ShouldBeOfType<ResolvedAgent>().Agent;

        var result = await agent.CompactAsync(
            engine.Conversation.SessionId.ShouldNotBeNull(),
            engine.Conversation.BranchId.ShouldNotBeNull(),
            engine.Identity,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<CompactionRejected>().Rejection.Kind.ShouldBe(CompactionRejectionKind.Disabled);
        log.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void WithDurability_WhenBuilderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((AgentEngineBuilder) null!).WithDurability()).ParamName.ShouldBe("builder");

    [Fact]
    public async Task WithDurability_WhenBuilt_SelectsAProfileEnablingEveryFirstPartyBoundaryAndTurnsStillComplete()
    {
        var handler = new StubOpenAIHandler("fine");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithDurability();
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

        reply.ShouldBe("fine");
        var snapshot = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);
        var definition = snapshot.Definitions.ShouldHaveSingleItem();
        var profileKey = definition.OptionalCapabilities.DurabilityProfile.ShouldNotBeNull();
        engine.Services.GetRequiredService<IDurabilityProfileCatalog>()
            .TryGet(profileKey, out var profile).ShouldBeTrue();
        profile.ShouldNotBeNull().EnabledOperations.ShouldBe(
            [
                LoopDurableOperations.ModelRequest,
                LoopDurableOperations.ToolCall,
                IoDurableOperations.InputPromotion,
                IoDurableOperations.RunSettlement,
                CompactionDurableOperations.Activation,
                PermissionsDurableOperations.ApprovalWait,
                EngineDurableOperations.RunAdmission,
            ],
            ignoreOrder: true);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithDurability_WhenComposedWithCompactionInEitherOrder_RegistersAHandlerForEveryEnabledBoundaryExactlyOnce(
        bool durabilityFirst)
    {
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini");
        _ = durabilityFirst ? builder.WithDurability().WithCompaction() : builder.WithCompaction().WithDurability();
        await using var engine = builder.Build();

        var handled = engine.Services.GetServices<IDurableOperationHandler>().Select(static handler => handler.OperationName).ToArray();
        var profileKey = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken))
            .Definitions.Single().OptionalCapabilities.DurabilityProfile.ShouldNotBeNull();
        engine.Services.GetRequiredService<IDurabilityProfileCatalog>().TryGet(profileKey, out var profile).ShouldBeTrue();

        handled.Distinct().Count().ShouldBe(handled.Length);
        profile.ShouldNotBeNull().EnabledOperations.ShouldBeSubsetOf(handled);
    }

    [Fact]
    public async Task WithDurability_WhenATurnCompletes_JournalsAdmissionModelRequestAndSettlementWithoutTheirContent()
    {
        const string prompt = "a-prompt-that-must-never-reach-a-durable-record";
        var (engine, journal, handler) = DurableEngine("fine");
        await using var owner = engine;

        var reply = await engine.AskAsync(prompt, TestContext.Current.CancellationToken);

        reply.ShouldBe("fine");
        // The engine composes first-party IO, so the loop's input-promotion boundary (once per promotion point of a
        // one-turn run) is journaled alongside admission, the model request, and settlement.
        DurableOperationName[] expectedOperations =
        [
            EngineDurableOperations.RunAdmission,
            IoDurableOperations.InputPromotion,
            IoDurableOperations.InputPromotion,
            IoDurableOperations.InputPromotion,
            LoopDurableOperations.ModelRequest,
            IoDurableOperations.RunSettlement,
        ];
        journal.Descriptors.Select(static descriptor => descriptor.Name).ShouldBe(expectedOperations, ignoreOrder: true);
        journal.Terminals.ShouldBe(expectedOperations, ignoreOrder: true);
        journal.Checkpoints.ShouldContain((EngineDurableOperations.RunAdmission, DurableCheckpointKind.InputAdmitted));
        journal.Checkpoints.ShouldContain((IoDurableOperations.RunSettlement, DurableCheckpointKind.RunSettled));
        foreach (var descriptor in journal.Descriptors)
        {
            Encoding.UTF8.GetString(descriptor.Input.Data.AsSpan()).ShouldNotContain(prompt);
        }

        handler.Bodies.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WithDurability_WhenAFinishedTurnIsRecovered_CommitsTheRecordedResultsWithoutCallingTheProviderAgain()
    {
        var (engine, journal, handler) = DurableEngine("fine");
        await using var owner = engine;
        _ = await engine.AskAsync("hello", TestContext.Current.CancellationToken);
        var coordinator = engine.Services.GetRequiredService<IDurableExecutionCoordinator>();
        var modelAttempt = journal.Descriptors.Single(static descriptor => descriptor.Name == LoopDurableOperations.ModelRequest);

        var recovered = await coordinator.RecoverAsync(
            modelAttempt.Address, modelAttempt.ExecutionContext, hooks: null, TestContext.Current.CancellationToken);

        recovered.State.ShouldBe(DurableOperationState.Completed);
        handler.Bodies.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WithDurability_WhenTheProcessIsLostAfterTheProviderWasCalled_RecoveryRequiresAnOperatorAndNeverRepeatsTheAttempt()
    {
        var (engine, journal, handler) = DurableEngine("fine");
        await using var owner = engine;
        journal.LoseTerminalOf = LoopDurableOperations.ModelRequest;

        var failure = await Record.ExceptionAsync(() => engine.AskAsync("hello", TestContext.Current.CancellationToken));

        _ = failure.ShouldNotBeNull();
        journal.Terminals.ShouldNotContain(LoopDurableOperations.ModelRequest);
        handler.Bodies.Count.ShouldBe(1);

        // A recovering worker finds a started, non-idempotent provider attempt with no terminal record. It cannot
        // know whether the provider acted, so replaying would risk a second charge; it escalates instead.
        journal.LoseTerminalOf = null;
        var lost = journal.Descriptors.Single(static descriptor => descriptor.Name == LoopDurableOperations.ModelRequest);
        var coordinator = engine.Services.GetRequiredService<IDurableExecutionCoordinator>();
        var escalation = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.RecoverAsync(
            lost.Address, lost.ExecutionContext, hooks: null, TestContext.Current.CancellationToken));

        escalation.Message.ShouldContain("operator action");
        handler.Bodies.Count.ShouldBe(1);
        journal.Terminals.ShouldNotContain(LoopDurableOperations.ModelRequest);
    }

    [Fact]
    public async Task WithDurability_WhenTheSettlementRecordIsLost_TheTurnStillReportsItsOwnOutcome()
    {
        var (engine, journal, _) = DurableEngine("fine");
        await using var owner = engine;
        journal.LoseTerminalOf = IoDurableOperations.RunSettlement;

        var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

        reply.ShouldBe("fine");
        journal.Terminals.ShouldNotContain(IoDurableOperations.RunSettlement);
        journal.Terminals.ShouldContain(LoopDurableOperations.ModelRequest);
    }

    private static (AgentEngine Engine, ProcessLossDurableOperationJournal Journal, StubOpenAIHandler Handler) DurableEngine(string reply)
    {
        // The simple sugar registers its in-memory journal with TryAdd under this key, so a journal registered first
        // wins and the inner journal is built exactly as the sugar would have built it.
        const string journalKey = "agentkit.simple.in-memory";
        var handler = new StubOpenAIHandler(reply);
        ProcessLossDurableOperationJournal? journal = null;
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini");
        _ = builder.Services.AddKeyedSingleton<IDurableOperationJournal>(journalKey, (provider, _) => journal ??=
            new ProcessLossDurableOperationJournal(new InMemoryDurableOperationJournal(
                new DurableJournalKey(journalKey),
                provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                provider.GetRequiredService<ISecurityAuditDispatcher>(),
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<InMemoryDurableOperationJournal>>())));
        _ = builder.WithDurability();
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        var engine = builder.Build();
        _ = engine.Services.GetRequiredKeyedService<IDurableOperationJournal>(journalKey);
        return (engine, journal!, handler);
    }

    [Fact]
    public void WithSqliteDurability_WhenArgumentsAreInvalid_ThrowsBeforeRegistering()
    {
        var builder = AgentEngine.CreateBuilder();

        Should.Throw<ArgumentNullException>(() => ((AgentEngineBuilder) null!).WithSqliteDurability("/tmp/x.db")).ParamName.ShouldBe("builder");
        Should.Throw<ArgumentException>(() => builder.WithSqliteDurability(" ")).ParamName.ShouldBe("databasePath");
        Should.Throw<ArgumentException>(() => builder.WithSqliteDurability("relative.db")).ParamName.ShouldBe("databasePath");
    }

    [Fact]
    public void WithJsonDurability_WhenArgumentsAreInvalid_ThrowsBeforeRegistering()
    {
        var builder = AgentEngine.CreateBuilder();

        Should.Throw<ArgumentNullException>(() => ((AgentEngineBuilder) null!).WithJsonDurability("/tmp/x")).ParamName.ShouldBe("builder");
        Should.Throw<ArgumentException>(() => builder.WithJsonDurability(" ")).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentException>(() => builder.WithJsonDurability("relative")).ParamName.ShouldBe("directoryPath");
    }

    [Fact]
    public void WithDurability_WhenAnotherStorageWasAlreadySelected_ThrowsInvalidOperationException()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentkit-simple-" + Guid.NewGuid().ToString("N"));
        var builder = AgentEngine.CreateBuilder().WithSqliteDurability(Path.Combine(root, "d.db"));

        Should.Throw<InvalidOperationException>(() => builder.WithDurability()).Message.ShouldContain("sqlite");
        _ = Should.Throw<InvalidOperationException>(() => builder.WithJsonDurability(Path.Combine(root, "json")));
        _ = builder.WithSqliteDurability(Path.Combine(root, "d.db"));
    }

    [Fact]
    public async Task WithSqliteDurability_WhenATurnCompletes_PersistsTheJournalInTheNamedDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "agentkit-simple-" + Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(root, "nested", "durable.db");
        try
        {
            var handler = new StubOpenAIHandler("fine");
            var builder = AgentEngine.CreateBuilder()
                .UseLocalDevelopmentDefaults()
                .UseOpenAI("sk-test", "gpt-4o-mini")
                .WithSqliteDurability(databasePath);
            _ = builder.Services.ReplaceNetworkWithHandler(handler);
            await using (var engine = builder.Build())
            {
                var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

                reply.ShouldBe("fine");
                _ = engine.Services.GetRequiredKeyedService<IDurableOperationJournal>("agentkit.simple.sqlite")
                    .ShouldBeOfType<Durability.Sqlite.SqliteDurableOperationJournal>();
            }

            File.Exists(databasePath).ShouldBeTrue();
            new FileInfo(databasePath).Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            DeleteQuietly(root);
        }
    }

    [Fact]
    public async Task WithJsonDurability_WhenATurnCompletes_PersistsTheJournalUnderTheNamedDirectory()
    {
        // The JSON store refuses any root that traverses a link, and macOS reaches its temp directory through one.
        var temp = OperatingSystem.IsMacOS() && Path.GetTempPath().StartsWith("/var/", StringComparison.Ordinal)
            ? "/private" + Path.GetTempPath()
            : Path.GetTempPath();
        var root = Path.Combine(temp, "agentkit-simple-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "journal");
        try
        {
            var handler = new StubOpenAIHandler("fine");
            var builder = AgentEngine.CreateBuilder()
                .UseLocalDevelopmentDefaults()
                .UseOpenAI("sk-test", "gpt-4o-mini")
                .WithJsonDurability(directory);
            _ = builder.Services.ReplaceNetworkWithHandler(handler);
            await using (var engine = builder.Build())
            {
                var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

                reply.ShouldBe("fine");
                _ = engine.Services.GetRequiredKeyedService<IDurableOperationJournal>("agentkit.simple.json")
                    .ShouldBeOfType<Durability.Json.JsonDurableOperationJournal>();
            }

            Directory.Exists(directory).ShouldBeTrue();
            Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories).ShouldNotBeEmpty();
        }
        finally
        {
            DeleteQuietly(root);
        }
    }

    private static void DeleteQuietly(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup; a lingering handle must not fail the test.
        }
    }

    [Fact]
    public void WithDelegation_WhenBuilderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((AgentEngineBuilder) null!).WithDelegation()).ParamName.ShouldBe("builder");

    [Fact]
    public async Task WithDelegation_WhenTheLeadDelegates_TheSpecialistRunsOnTheSameEngineAndItsAnswerReturnsThroughTheTool()
    {
        var specialist = new AgentId(Guid.Parse("7a000000-0000-0000-0000-000000000009"));
        var delegation = $$"""tool:task:{"target_agent_id":"{{specialist.Value}}","objective":"Find the retry policy.","acceptance_criteria":["Name the file."],"allowed_tools":[]}""";
        // Turn 1 of the lead: call task. The specialist's single turn answers. Turn 2 of the lead: final answer.
        var handler = new StubOpenAIHandler(delegation, "It is in RetryPolicy.cs.", "The specialist found it in RetryPolicy.cs.");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithInstructions("You are the lead.")
            .AddAgent(specialist, static o =>
            {
                o.DisplayName = "specialist";
                o.Instructions.Add("You are the specialist.");
                o.IncludeRegisteredTools = false;
            })
            .WithDelegation();
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        var result = await engine.SendAsync("Where is the retry policy?", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        var toolResult = result.Events.OfType<ConversationToolResultEvent>().ShouldHaveSingleItem();
        toolResult.ToolName.ShouldBe("task");
        toolResult.Succeeded.ShouldBeTrue(customMessage: toolResult.Summary);
        handler.Bodies.Count.ShouldBe(3);
        handler.Bodies[0].ShouldContain("\"name\":\"task\"");
        handler.Bodies[1].ShouldContain("You are the specialist.");
        handler.Bodies[1].ShouldContain("Find the retry policy.");
        handler.Bodies[1].ShouldNotContain("\"name\":\"task\"");
        handler.Bodies[2].ShouldContain("RetryPolicy.cs");
        var specialistSessions = await (await engine.GetAgentAsync(specialist, TestContext.Current.CancellationToken)).ShouldBeOfType<ResolvedAgent>().Agent
            .SendAsync(new AgentSendRequest(engine.Identity, "and now?"), TestContext.Current.CancellationToken);
        _ = specialistSessions.Outcome.ShouldBeOfType<RunSucceeded>();
    }

    [Fact]
    public async Task WithDelegation_WhenBuilt_EveryDefinitionSelectsThePublishedGoalProfileAndTheWorkerIsHosted()
    {
        var specialist = new AgentId(Guid.Parse("7a000000-0000-0000-0000-00000000000a"));
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .AddAgent(specialist, static o => o.DisplayName = "specialist")
            .WithDelegation();
        _ = builder.Services.ReplaceNetworkWithHandler(new StubOpenAIHandler("fine"));
        await using var engine = builder.Build();

        var snapshot = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);

        snapshot.Definitions.Length.ShouldBe(2);
        foreach (var definition in snapshot.Definitions)
        {
            var key = definition.OptionalCapabilities.GoalProfile.ShouldNotBeNull();
            engine.Services.GetRequiredService<IGoalProfileCatalog>().TryGet(key, out var profile).ShouldBeTrue();
            profile.ShouldNotBeNull().StoreKey.Value.ShouldBe("agentkit.simple.in-memory");
        }

        engine.Services.GetServices<IHostedService>().ShouldContain(static service => service is GoalDelegationWorker);
        _ = engine.Services.GetRequiredService<IAgentMessageChannel>().ShouldBeOfType<EngineAgentMessageChannel>();
    }

    [Fact]
    public void WithArtifacts_WhenBuilderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((AgentEngineBuilder) null!).WithArtifacts()).ParamName.ShouldBe("builder");

    [Fact]
    public void WithArtifacts_WhenConfigurationIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().WithArtifacts(static o => o.MaximumArtifactBytes = 0)).ParamName.ShouldBe("configure");
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().WithArtifacts(static o => o.ProcessOutputClassification = (DataClassification) 99)).ParamName.ShouldBe("configure");
    }

    [Fact]
    public async Task WithArtifacts_WhenBuilt_EveryDefinitionSelectsTheCoordinatorAndTurnsStillComplete()
    {
        var specialist = new AgentId(Guid.Parse("7a000000-0000-0000-0000-00000000000c"));
        var handler = new StubOpenAIHandler("fine");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .AddAgent(specialist, static o => o.DisplayName = "specialist")
            .WithArtifacts();
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

        reply.ShouldBe("fine");
        var snapshot = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);
        snapshot.Definitions.Length.ShouldBe(2);
        foreach (var definition in snapshot.Definitions)
        {
            var key = definition.OptionalCapabilities.ArtifactCoordinator.ShouldNotBeNull();
            _ = engine.Services.GetRequiredKeyedService<IArtifactCoordinator>(key.Value);
            engine.Services.GetRequiredService<IArtifactCoordinatorCatalog>().TryGet(key, out var coordinator).ShouldBeTrue();
            coordinator!.Backends.ShouldBe([new ArtifactBackendKey("agentkit.simple.in-memory")]);
        }
    }

    [Fact]
    public void WithMemory_WhenBuilderIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((AgentEngineBuilder) null!).WithMemory()).ParamName.ShouldBe("builder");

    [Fact]
    public void WithMemory_WhenClassificationIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().WithMemory(static o => o.MaximumClassification = (DataClassification) 99)).ParamName.ShouldBe("configure");

    [Fact]
    public async Task WithMemory_WhenBuilt_EveryDefinitionSelectsTheCompiledProfileAndTurnsStillComplete()
    {
        var specialist = new AgentId(Guid.Parse("7a000000-0000-0000-0000-00000000000b"));
        var handler = new StubOpenAIHandler("fine");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .AddAgent(specialist, static o => o.DisplayName = "specialist")
            .WithMemory();
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

        reply.ShouldBe("fine");
        var snapshot = await engine.GetAgentsAsync(TestContext.Current.CancellationToken);
        snapshot.Definitions.Length.ShouldBe(2);
        foreach (var definition in snapshot.Definitions)
        {
            var key = definition.OptionalCapabilities.MemoryProfile.ShouldNotBeNull();
            engine.Services.GetRequiredService<IMemoryProfileCatalog>().TryGet(key, out var profile).ShouldBeTrue();
            var compiled = profile.ShouldNotBeNull();
            compiled.MemoryStore.ShouldBe(new MemoryStoreKey("agentkit.simple.in-memory"));
            compiled.RetrievalSources.ShouldBe([MemoryRetrievalSourceKeys.DurableMemory]);
            compiled.MaximumClassification.ShouldBe(DataClassification.Internal);
        }
    }

    [Theory]
    [InlineData(false, MemoryAcceptanceMode.RequireExplicitPolicyAllow)]
    [InlineData(true, MemoryAcceptanceMode.AllowUnlessPolicyDenies)]
    public async Task WithMemory_WhenConfigured_AppliesTheAcceptanceRuleAndStaysFailClosedByDefault(bool accept, MemoryAcceptanceMode expected)
    {
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithMemory(o => o.AcceptProposals = accept);
        _ = builder.Services.ReplaceNetworkWithHandler(new StubOpenAIHandler("fine"));
        await using var engine = builder.Build();

        engine.Services.GetRequiredService<IOptions<AgentMemoryOptions>>().Value.AcceptanceMode.ShouldBe(expected);
    }

    [Fact]
    public async Task Build_WhenNoBudgetIsConfigured_ComposesAnUnlimitedDefaultProfileEveryAgentSelects()
    {
        await using var engine = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .Build();

        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions.Single();

        definition.Components.BudgetProfile.ShouldBe(AgentBudgetComponentDefaults.ProfileKey);
        engine.Services.GetRequiredService<IBudgetProfileCatalog>().TryGet(definition.Components.BudgetProfile, out var profile).ShouldBeTrue();
        profile!.Limits.ShouldBeEmpty();
    }

    [Fact]
    public async Task WithBudget_WhenAnAdditionalAgentIsHosted_BothAgentsSelectTheSameLimitedProfile()
    {
        await using var engine = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithBudget(static o => o.MaxTurns = 4)
            .AddAgent(new AgentId(Guid.NewGuid()), static o => o.Instructions.Add("Specialist."))
            .Build();

        var definitions = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions;
        var catalog = engine.Services.GetRequiredService<IBudgetProfileCatalog>();

        definitions.Length.ShouldBe(2);
        foreach (var definition in definitions)
        {
            definition.Components.BudgetProfile.ShouldBe(AgentBudgetComponentDefaults.ProfileKey);
            catalog.TryGet(definition.Components.BudgetProfile, out var profile).ShouldBeTrue();
            profile!.Limits.ShouldHaveSingleItem().Dimension.ShouldBe(BudgetDimensions.Turns);
        }
    }

    [Fact]
    public async Task Build_WhenCalled_SelectsAndComposesTheFirstPartyKeyedCollaboratorsForEveryDefinition()
    {
        // Composition validation proves every selected key resolves to exactly one registration, so a successful
        // build is the evidence that the keyed input, output, processor, context, selector, and executor exist.
        await using var engine = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .AddAgent(new AgentId(Guid.NewGuid()), static o => o.Instructions.Add("Specialist."))
            .Build();

        var definitions = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions;

        definitions.Length.ShouldBe(2);
        definitions.ShouldAllBe(static definition => definition.Components.Equals(SimpleAgentPlan.DefaultComponents()));
    }

    [Fact]
    public void WithBudget_WhenConfigureIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => AgentEngine.CreateBuilder().WithBudget(null!)).ParamName.ShouldBe("configure");

    [Fact]
    public void WithBudget_WhenNoLimitIsConfigured_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => AgentEngine.CreateBuilder().WithBudget(static _ => { })).ParamName.ShouldBe("configure");

    [Fact]
    public void WithBudget_WhenALimitIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => AgentEngine.CreateBuilder().WithBudget(static o => o.MaxCostUsd = 0m)).ParamName.ShouldBe("configure");

    [Fact]
    public async Task WithBudget_WhenTheToolCallBudgetIsExhausted_TheTurnFailsNamingTheDimensionAndTheToolIsNotInvoked()
    {
        var handler = new StubOpenAIHandler(
            "tool:read_file:{\"path\":\"a.txt\"}",
            "tool:read_file:{\"path\":\"b.txt\"}",
            "done");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithBudget(static o => o.MaxToolCalls = 1);
        RegisterInMemoryReadTool(builder);
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();
        engine.Services.GetRequiredService<InMemoryFileSystem>().Seed(new FileSystemPath("a.txt"), "A");
        engine.Services.GetRequiredService<InMemoryFileSystem>().Seed(new FileSystemPath("b.txt"), "B");

        var result = await engine.SendAsync("read both", TestContext.Current.CancellationToken);
        var definition = (await engine.GetAgentsAsync(TestContext.Current.CancellationToken)).Definitions.Single();

        result.Succeeded.ShouldBeTrue();
        var toolResults = result.Events.OfType<ConversationToolResultEvent>().ToList();
        toolResults.Count.ShouldBe(2);
        toolResults[0].Succeeded.ShouldBeTrue(customMessage: toolResults[0].Summary);
        toolResults[1].Succeeded.ShouldBeFalse();
        handler.Bodies[2].ShouldContain("not attempted");
        var profile = engine.Services.GetRequiredService<IBudgetProfileCatalog>();
        profile.TryGet(definition.Components.BudgetProfile, out var budgetProfile).ShouldBeTrue();
        budgetProfile!.Limits.ShouldHaveSingleItem().Dimension.ShouldBe(BudgetDimensions.AttemptedToolCalls);
    }

    [Fact]
    public async Task WithBudget_WhenTheTurnBudgetIsExhausted_AskAsyncThrowsNamingTheDimension()
    {
        var handler = new StubOpenAIHandler("tool:read_file:{\"path\":\"a.txt\"}", "done");
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithBudget(static o => o.MaxTurns = 1);
        RegisterInMemoryReadTool(builder);
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();
        engine.Services.GetRequiredService<InMemoryFileSystem>().Seed(new FileSystemPath("a.txt"), "A");

        var exception = await Should.ThrowAsync<SimpleAgentException>(() => engine.AskAsync("read it", TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("agentkit.turns");
        handler.Bodies.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Build_WhenAnotherProviderIsRegisteredOnServices_UseModelSelectsIt()
    {
        var handler = new StubOpenAIHandler("via alias");
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults();
        _ = builder.Services.AddOpenAI();
        _ = builder.Services.AddOpenAIApiKeyCredential("sk-test");
        _ = builder.Services.AddOpenAIKnownLlmModel(new ModelAlias("custom"), new ModelId("gpt-4o"));
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
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

        (await engine.GetAgentAsync(agentId, TestContext.Current.CancellationToken)).ShouldBeOfType<ResolvedAgent>().Agent.Definition.Id.ShouldBe(agentId);
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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
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
    public async Task UseWorkspace_WhenTheModelReadsAFile_RecordsTheAcceptedCallBeforeInvocationAndTheTerminalOutcomeAfterIt()
    {
        using var directory = new TempDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "notes.txt"), "the answer is 42", TestContext.Current.CancellationToken);
        var handler = new StubOpenAIHandler("tool:read_file:{\"path\":\"notes.txt\"}", "It says 42.");
        var collector = new ToolEventCollector();
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .UseWorkspace(directory.Path);
        _ = builder.Services.AddSingleton(collector);
        _ = builder.Services.AddToolEventSink<CollectingToolEventSink>(new ToolEventSinkRegistration(new ComponentId("simple.tests"), 0));
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        var result = await engine.SendAsync("what do my notes say?", TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        collector.Events.Select(static item => item.GetType()).ShouldBe([typeof(ToolCallAcceptedEvent), typeof(ToolCallTerminalEvent)]);
        var terminal = collector.Events.OfType<ToolCallTerminalEvent>().Single();
        terminal.Status.ShouldBe(ToolTerminalStatus.Succeeded);
        terminal.Accepted.ShouldBeTrue();
        terminal.Recorded.ShouldBeTrue();
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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
        await using var engine = builder.Build();

        var result = await engine.SendAsync("write a file", TestContext.Current.CancellationToken);

        result.Events.OfType<ConversationToolResultEvent>().ShouldHaveSingleItem().Succeeded.ShouldBeFalse();
        File.Exists(Path.Combine(directory.Path, "out.txt")).ShouldBeFalse();
    }

    private sealed class ToolEventCollector
    {
        private readonly List<ToolEvent> _events = [];
        private readonly Lock _gate = new();

        public IReadOnlyList<ToolEvent> Events
        {
            get
            {
                lock (_gate)
                {
                    return [.. _events];
                }
            }
        }

        public void Add(ToolEvent toolEvent)
        {
            lock (_gate)
            {
                _events.Add(toolEvent);
            }
        }
    }

    private sealed class CollectingToolEventSink(ToolEventCollector collector): IToolEventSink
    {
        public ValueTask PublishAsync(ToolEvent toolEvent, CancellationToken cancellationToken = default)
        {
            collector.Add(toolEvent);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>The policy shown in docs/guides/permissions.md.</summary>
    private sealed class ReadOnlyWorkspacePolicy: ISecurityPolicy
    {
        public ValueTask<SecurityPolicyResult> EvaluateAsync(
            SecurityRequest request,
            SecurityPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(context);
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
        _ = builder.Services.ReplaceNetworkWithHandler(handler);
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

    private static void RegisterInMemoryReadTool(AgentEngineBuilder builder)
    {
        var services = builder.Services;
        var workspaceProfile = new FileSystemProfileKey("workspace");
        var workspaceRoot = new FileRootId("workspace");
        _ = services.AddInMemoryFileSystem(workspaceProfile);
        services.TryAddSingleton(provider => provider.GetRequiredKeyedService<InMemoryFileSystem>(workspaceProfile.Value));
        _ = services.AddReadTool(o =>
        {
            o.ProfileKey = workspaceProfile;
            o.RootId = workspaceRoot;
            o.HostRootPath = Path.GetTempPath();
        });
        _ = builder.WithTools(ReadFileTool.DefaultToolset.Key);
    }

    private sealed class VetoSecretsHook: IBeforeToolInvocationHook
    {
        public ValueTask InvokeAsync(
            BeforeToolInvocationEventArgs args,
            HookInvocationContext context,
            CancellationToken cancellationToken = default)
        {
            if (args.Arguments.TryGetProperty("path", out var path) && path.GetString()!.Contains("secret", StringComparison.Ordinal))
            {
                args.Veto = new ToolInvocationVeto("secrets stay secret");
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Collects the requests a <see cref="CapturingCompactor"/> receives, shared through dependency injection.</summary>
    private sealed class CapturedCompactionRequests
    {
        public List<CompactionRequest> Requests { get; } = [];
    }

    /// <summary>A compactor that records its request and declines to compact, so the run continues unchanged.</summary>
    private sealed class CapturingCompactor(CapturedCompactionRequests log): ICompactor
    {
        public Task<CompactionResult> CompactAsync(CompactionRequest request, CancellationToken cancellationToken = default)
        {
            log.Requests.Add(request);
            return Task.FromResult<CompactionResult>(
                new CompactionNotReducing(request.Context, new CompactionSizeEstimate(1, 1, 1), new CompactionSizeEstimate(1, 1, 1), 0.1));
        }

        public Task<CompactionResult> CompactAsync(
            CompactionRequest request,
            SessionExecutionCapability session,
            BudgetExecutionCapability budget,
            HookDispatchContext? hooks,
            CancellationToken cancellationToken = default) => CompactAsync(request, cancellationToken);
    }

    [Fact]
    public void UseOpenAI_WhenBuilt_ComposesTheNetworkBoundaryUnderTheDefaultInternetOnlyPolicy()
    {
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini");
        using var provider = builder.Services.BuildServiceProvider();

        var policy = provider.GetRequiredService<IOptions<AgentNetworkOptions>>().Value.DestinationPolicy;

        policy.ShouldBe(NetworkDestinationPolicy.Default);
        builder.Services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(INetworkTransport));
        builder.Services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(HttpClient));
    }

    [Fact]
    public void UseOllama_WhenBuilt_ComposesTheNetworkBoundaryWithAPolicyAllowingLocalEndpoints()
    {
        var builder = AgentEngine.CreateBuilder().UseLocalDevelopmentDefaults().UseOllama("llama3.1:8b");
        using var provider = builder.Services.BuildServiceProvider();

        var policy = provider.GetRequiredService<IOptions<AgentNetworkOptions>>().Value.DestinationPolicy;

        policy.AllowPrivateAddresses.ShouldBeTrue();
        policy.AllowedSchemes.ShouldBe(["http", "https"]);
        policy.AllowedHosts.ShouldBeNull();
    }

    [Fact]
    public async Task UseOpenAI_WhenNetworkIsReplacedWithTheScriptedLeaf_CompletesATurnThroughGrantedProviderEgress()
    {
        var destination = new NetworkDestination("https", new NormalizedHost("api.openai.com"), 443, new NetworkRoute("/v1/chat/completions"));
        const string body = /*lang=json,strict*/ """{"id":"c","object":"chat.completion","created":1,"model":"gpt-4o-mini","choices":[{"index":0,"message":{"role":"assistant","content":"scripted hello"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""";
        var builder = AgentEngine.CreateBuilder()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini", options => options.PreferStreaming = false);
        _ = builder.Services.RemoveAll<INetworkNameResolver>();
        _ = builder.Services.AddSingleton<INetworkNameResolver>(provider =>
        {
            var resolver = new ScriptedNetworkNameResolver(
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>());
            var now = provider.GetRequiredService<TimeProvider>().GetUtcNow();
            resolver.Script(destination, new NetworkResolved([new NetworkAddress(IPAddress.Parse("93.184.216.34"), now, now.AddHours(1))]));
            return resolver;
        });
        _ = builder.Services.RemoveAll<INetworkTransport>();
        _ = builder.Services.AddSingleton<INetworkTransport>(provider =>
        {
            var transport = new ScriptedNetworkTransport(
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>());
            var headers = new NetworkHeaderSet([new NetworkHeader("Content-Type", "application/json")]);
            transport.Script(
                destination,
                new NetworkResponseReceived(new ScriptedNetworkResponse(new NetworkResponseMetadata(200, headers, null), Encoding.UTF8.GetBytes(body))));
            return transport;
        });
        await using var engine = builder.Build();

        var reply = await engine.AskAsync("hello", TestContext.Current.CancellationToken);

        reply.ShouldBe("scripted hello");
        var transportTrace = engine.Services.GetRequiredService<INetworkTransport>().ShouldBeOfType<ScriptedNetworkTransport>().Traces;
        transportTrace.ShouldHaveSingleItem().Destination.ShouldBe(destination);
        _ = engine.Services.GetRequiredService<INetworkNameResolver>().ShouldBeOfType<ScriptedNetworkNameResolver>().Traces.ShouldHaveSingleItem();
    }
}

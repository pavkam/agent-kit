// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

using AgentKit.FileSystem.InMemory;
using AgentKit.Providers.OpenAI;
using AgentKit.Tools.Read;

/// <summary>Verifies SimpleAgentBuilder behavior and contracts.</summary>
public sealed class SimpleAgentBuilderTests
{
    [Fact]
    public void Create_WhenCalled_StartsWithAnEmptyServiceCollection() =>
        SimpleAgentBuilder.Create().Services.ShouldBeEmpty();

    [Theory]
    [InlineData(null, "gpt-4o-mini", "apiKey")]
    [InlineData(" ", "gpt-4o-mini", "apiKey")]
    [InlineData("sk-test", null, "modelId")]
    [InlineData("sk-test", "", "modelId")]
    public void UseOpenAI_WhenAnArgumentIsBlank_ThrowsArgumentExceptionBeforeRegistering(string? apiKey, string? modelId, string parameter)
    {
        var builder = SimpleAgentBuilder.Create();

        Should.Throw<ArgumentException>(() => builder.UseOpenAI(apiKey!, modelId!)).ParamName.ShouldBe(parameter);
        builder.Services.ShouldBeEmpty();
    }

    [Fact]
    public void UseOpenAI_WhenModelIsUnknown_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => SimpleAgentBuilder.Create().UseOpenAI("sk-test", "gpt-imaginary")).ParamName.ShouldBe("modelId");

    [Fact]
    public void UseModel_WhenAliasIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SimpleAgentBuilder.Create().UseModel(default)).ParamName.ShouldBe("alias");

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void WithInstructions_WhenTextIsBlank_ThrowsArgumentException(string? text) =>
        Should.Throw<ArgumentException>(() => SimpleAgentBuilder.Create().WithInstructions(text!)).ParamName.ShouldBe("text");

    [Fact]
    public void WithIdentity_WhenNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SimpleAgentBuilder.Create().WithIdentity(null!)).ParamName.ShouldBe("identity");

    [Fact]
    public void WithAgentId_WhenDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => SimpleAgentBuilder.Create().WithAgentId(default)).ParamName.ShouldBe("agentId");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WithMaxTurns_WhenNotPositive_ThrowsArgumentOutOfRangeException(int maxTurns) =>
        Should.Throw<ArgumentOutOfRangeException>(() => SimpleAgentBuilder.Create().WithMaxTurns(maxTurns)).ParamName.ShouldBe("maxTurns");

    [Fact]
    public void WithAttemptTimeout_WhenZero_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => SimpleAgentBuilder.Create().WithAttemptTimeout(TimeSpan.Zero)).ParamName.ShouldBe("timeout");

    [Fact]
    public void WithRequestSettings_WhenNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => SimpleAgentBuilder.Create().WithRequestSettings(null!)).ParamName.ShouldBe("settings");

    [Fact]
    public void Build_WhenNoModelWasSelected_ThrowsInvalidOperationExceptionNamingTheFix()
    {
        var exception = Should.Throw<InvalidOperationException>(() => SimpleAgentBuilder.Create().UseLocalDevelopmentDefaults().Build());

        exception.Message.ShouldContain("UseOpenAI");
        exception.Message.ShouldContain("UseModel");
    }

    [Fact]
    public void Build_WhenNoIdentityAndNoLocalDefaults_ThrowsInvalidOperationExceptionNamingTheFix()
    {
        var exception = Should.Throw<InvalidOperationException>(() => SimpleAgentBuilder.Create().UseOpenAI("sk-test", "gpt-4o-mini").Build());

        exception.Message.ShouldContain("WithIdentity");
        exception.Message.ShouldContain("UseLocalDevelopmentDefaults");
    }

    [Fact]
    public void Build_WhenIdentityIsSuppliedButNoStorageOrSecurity_ThrowsInvalidOperationExceptionNamingTheMissingPieces()
    {
        var builder = SimpleAgentBuilder.Create()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithIdentity(TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human));

        var exception = Should.Throw<InvalidOperationException>(builder.Build);

        exception.Message.ShouldContain("session store");
        exception.Message.ShouldContain("UseLocalDevelopmentDefaults");
        _ = exception.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public void Build_WhenCalledTwice_ThrowsInvalidOperationException()
    {
        var builder = SimpleAgentBuilder.Create().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini");
        using var first = builder.Build();

        _ = Should.Throw<InvalidOperationException>(builder.Build);
        _ = Should.Throw<InvalidOperationException>(() => builder.WithMaxTurns(2));
    }

    [Fact]
    public void Build_WhenLocalDefaultsAndOpenAI_ProducesAnAgentWithoutTouchingTheNetwork()
    {
        using var agent = SimpleAgentBuilder.Create()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .Build();

        _ = agent.Conversation.ShouldBeAssignableTo<IConversationSession>();
    }

    [Fact]
    public async Task Build_WhenServicesRegisterATool_AdvertisesItToTheModel()
    {
        var handler = new StubOpenAIHandler("done");
        var builder = SimpleAgentBuilder.Create().UseLocalDevelopmentDefaults().UseOpenAI("sk-test", "gpt-4o-mini");
        _ = builder.Services.AddInMemoryFileSystem();
        _ = builder.Services.AddReadTool();
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        using var agent = builder.Build();

        _ = await agent.AskAsync("hi", TestContext.Current.CancellationToken);

        handler.Bodies.Single().ShouldContain("\"tools\"");
        handler.Bodies.Single().ShouldContain("\"name\":\"read_file\"");
    }

    [Fact]
    public async Task Build_WhenInstructionsAndSettingsAreSupplied_SendsThemInOrder()
    {
        var handler = new StubOpenAIHandler("ok");
        var builder = SimpleAgentBuilder.Create()
            .UseLocalDevelopmentDefaults()
            .UseOpenAI("sk-test", "gpt-4o-mini")
            .WithInstructions("First rule.")
            .WithInstructions("Second rule.")
            .WithRequestSettings(LlmRequestSettings.Default with { Temperature = 0.2 })
            .WithMaxTurns(3);
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        using var agent = builder.Build();

        _ = await agent.AskAsync("hello", TestContext.Current.CancellationToken);

        var body = handler.Bodies.Single();
        body.IndexOf("First rule.", StringComparison.Ordinal).ShouldBeLessThan(body.IndexOf("Second rule.", StringComparison.Ordinal));
        body.ShouldContain("\"temperature\":0.2");
        body.ShouldContain("\"model\":\"gpt-4o-mini\"");
        handler.Requests.Single().Headers.Authorization.ShouldNotBeNull().Parameter.ShouldBe("sk-test");
    }

    [Fact]
    public async Task Build_WhenAnotherProviderIsRegisteredOnServices_UseModelSelectsIt()
    {
        var handler = new StubOpenAIHandler("via alias");
        var builder = SimpleAgentBuilder.Create().UseLocalDevelopmentDefaults();
        _ = builder.Services.AddOpenAI();
        _ = builder.Services.AddOpenAIApiKeyCredential("sk-test");
        _ = builder.Services.AddOpenAIKnownLlmModel(new ModelAlias("custom"), new ModelId("gpt-4o"));
        _ = builder.Services.Replace(ServiceDescriptor.Singleton(new HttpClient(handler)));
        using var agent = builder.UseModel(new ModelAlias("custom")).Build();

        var reply = await agent.AskAsync("hello", TestContext.Current.CancellationToken);

        reply.ShouldBe("via alias");
        handler.Bodies.Single().ShouldContain("\"model\":\"gpt-4o\"");
    }
}

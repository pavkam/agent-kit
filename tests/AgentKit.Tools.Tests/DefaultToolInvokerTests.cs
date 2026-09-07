// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

public sealed class DefaultToolInvokerTests
{
    [Fact]
    public void Constructor_WhenCatalogNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DefaultToolInvoker(null!, new AllowListToolAuthorizer(Options.Create(new AgentToolsOptions()))));

        exception.ParamName.ShouldBe("catalog");
    }

    [Fact]
    public void Constructor_WhenAuthorizerNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultToolInvoker(new ToolCatalog([]), null!));

        exception.ParamName.ShouldBe("authorizer");
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestNull_ThrowsArgumentNullException()
    {
        var invoker = CreateInvoker([]);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => invoker.InvokeAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task InvokeAsync_WhenToolNotRegistered_ReturnsRejected()
    {
        var invoker = CreateInvoker([]);
        var request = TestFactory.CallRequest(new ToolId("missing"));

        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.FailureReason.ShouldNotBeNull().ShouldContain("missing");
    }

    [Fact]
    public async Task InvokeAsync_WhenToolNotAuthorized_ReturnsRejectedWithoutInvokingTool()
    {
        var tool = new FakeTool { Descriptor = TestFactory.Descriptor("guarded") };
        var invoker = CreateInvoker([tool]); // no tool ids allow-listed
        var request = TestFactory.CallRequest(new ToolId("guarded"));

        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        tool.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorized_InvokesToolAndReturnsItsResult()
    {
        var expectedContent = ImmutableArray.Create<ContentPart>(new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty));
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("allowed"),
            OnInvoke = (_, _) => Task.FromResult(new ToolInvocationResult(
                new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty), expectedContent))
        };
        var invoker = CreateInvoker([tool], allowed: "allowed");
        var request = TestFactory.CallRequest(new ToolId("allowed"));

        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        result.Content.ShouldBe(expectedContent);
        _ = tool.ReceivedRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task InvokeAsync_WhenToolThrows_ReturnsFailed()
    {
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("throws"),
            OnInvoke = (_, _) => throw new InvalidOperationException("boom")
        };
        var invoker = CreateInvoker([tool], allowed: "throws");
        var request = TestFactory.CallRequest(new ToolId("throws"));

        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
    }

    [Fact]
    public async Task InvokeAsync_WhenCancelled_PropagatesOperationCanceledException()
    {
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("cancels"),
            OnInvoke = (_, ct) => Task.FromCanceled<ToolInvocationResult>(ct)
        };
        var invoker = CreateInvoker([tool], allowed: "cancels");
        var request = TestFactory.CallRequest(new ToolId("cancels"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => invoker.InvokeAsync(request, cts.Token));
    }

    [Fact]
    public async Task InvokeAsync_WhenToolThrowsOperationCanceledExceptionWithoutCallerCancellation_ReturnsFailed()
    {
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("cancels-itself"),
            OnInvoke = (_, _) => throw new OperationCanceledException("The tool canceled itself.")
        };
        var invoker = CreateInvoker([tool], allowed: "cancels-itself");
        var request = TestFactory.CallRequest(new ToolId("cancels-itself"));

        var result = await invoker.InvokeAsync(request, CancellationToken.None);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason.ShouldNotBeNull().ShouldContain("cancels-itself");
    }

    [Fact]
    public async Task InvokeAsync_WhenObserved_EmitsCorrelatedContentFreeActivity()
    {
        const string protectedArguments = "do-not-export-this-argument";
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity => stopped = activity,
        };
        ActivitySource.AddActivityListener(listener);
        var tool = new FakeTool { Descriptor = TestFactory.Descriptor("observed") };
        var invoker = CreateInvoker([tool], allowed: "observed");
        using var arguments = JsonDocument.Parse($$"""{"secret":"{{protectedArguments}}"}""");
        var request = TestFactory.CallRequest(new ToolId("observed"), arguments.RootElement);

        _ = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);

        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.ExecuteTool);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.ToolCallId).ShouldBe(request.Context.ToolCallId.ToString());
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain(protectedArguments);
    }

    private static DefaultToolInvoker CreateInvoker(IEnumerable<ITool> tools, params string[] allowed)
    {
        var options = new AgentToolsOptions();
        foreach (var id in allowed)
        {
            _ = options.AllowedToolIds.Add(new ToolId(id));
        }

        return new DefaultToolInvoker(new ToolCatalog(tools), new AllowListToolAuthorizer(Options.Create(options)));
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded;
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Diagnostics.CodeAnalysis;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies DefaultToolInvoker behavior and contracts.</summary>
public sealed class DefaultToolInvokerTests
{
    [Fact]
    public void Constructor_WhenCatalogNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DefaultToolInvoker(null!, new AllowListToolAuthorizer(Options.Create(new AgentToolsOptions()))));
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
        var exception = await Should.ThrowAsync<ArgumentNullException>(() => invoker.InvokeAsync(null!, TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task InvokeAsync_WhenToolNotRegistered_ReturnsRejected()
    {
        var invoker = CreateInvoker([]);
        var request = TestFactory.CallRequest(new ToolId("missing"));
        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);
        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Invocation.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.UnknownTool);
        result.Invocation.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Invocation.Outcome.Retryable.ShouldBeFalse();
        result.Invocation.Outcome.FailureReason.ShouldNotBeNull().ShouldContain("missing");
        result.Tool.IsResolved.ShouldBeFalse();
        result.Tool.Id.ShouldBeNull();
        result.Tool.ProviderAlias.ShouldBe(new ToolAlias("missing"));
    }

    [Fact]
    public async Task InvokeAsync_WhenToolNotAuthorized_ReturnsRejectedWithoutInvokingTool()
    {
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("guarded")
        };
        var invoker = CreateInvoker([tool]); // no tool ids allow-listed
        var request = TestFactory.CallRequest(new ToolId("guarded"));
        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);
        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Invocation.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.Denied);
        result.Invocation.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Invocation.Outcome.Retryable.ShouldBeFalse();
        tool.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentsDoNotSatisfyTheDeclaredSchema_ReturnsInvalidArgumentsWithoutInvokingTool()
    {
        // AGENTS.md requires that every call passes schema validation before invocation, but nothing in the
        // invoker ever compiled a descriptor's declared InputSchema or validated a call's arguments against it.
        // Every feature tool relied solely on its own ad-hoc parsing, and declared schema constraints a tool
        // did not re-implement (here: additionalProperties: false and a required member) were silently
        // unenforced.
        using var schemaDocument = JsonDocument.Parse(
            /*lang=json,strict*/ """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}""");
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("schema-checked", ToolEffect.ReadOnly, schemaDocument.RootElement),
        };
        var invoker = CreateInvoker([tool], allowed: "schema-checked");
        using var argumentsDocument = JsonDocument.Parse( /*lang=json,strict*/ """{"unexpected":"value"}""");
        var request = TestFactory.CallRequest(new ToolId("schema-checked"), argumentsDocument.RootElement);

        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);

        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Invocation.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Invocation.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        tool.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentsSatisfyTheDeclaredSchema_InvokesTheTool()
    {
        using var schemaDocument = JsonDocument.Parse(
            /*lang=json,strict*/ """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}""");
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("schema-checked-valid", ToolEffect.ReadOnly, schemaDocument.RootElement),
        };
        var invoker = CreateInvoker([tool], allowed: "schema-checked-valid");
        using var argumentsDocument = JsonDocument.Parse( /*lang=json,strict*/ """{"path":"a.txt"}""");
        var request = TestFactory.CallRequest(new ToolId("schema-checked-valid"), argumentsDocument.RootElement);

        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);

        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        _ = tool.ReceivedRequests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorized_InvokesToolAndReturnsItsResult()
    {
        var expectedContent = ImmutableArray.Create<ContentPart>(new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty));
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("allowed"),
            OnInvoke = (_, _) => Task.FromResult(new ToolInvocationResult(new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), expectedContent))
        };
        var invoker = CreateInvoker([tool], allowed: "allowed");
        var request = TestFactory.CallRequest(new ToolId("allowed"));
        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);
        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        result.Invocation.Content.ShouldBe(expectedContent);
        result.Tool.IsResolved.ShouldBeTrue();
        result.Tool.Id.ShouldBe(new ToolId("allowed"));
        result.Tool.Version.ShouldBe(new ToolVersion("1.0"));
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
        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Invocation.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Invocation.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        result.Invocation.Outcome.Retryable.ShouldBeFalse();
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
        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Invocation.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Invocation.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.Unknown);
        result.Invocation.Outcome.Retryable.ShouldBeFalse();
        result.Invocation.Outcome.FailureReason.ShouldNotBeNull().ShouldContain("cancels-itself");
    }

    [Fact]
    public async Task InvokeAsync_WhenObserved_EmitsCorrelatedContentFreeActivity()
    {
        const string protectedArguments = "do-not-export-this-argument";
        using var parent = new Activity("observed-tool-invocation").Start();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parent.TraceId && activity.OperationName == AgentKitActivityNames.ExecuteTool) { stopped = activity; }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("observed")
        };
        var invoker = CreateInvoker([tool], allowed: "observed");
        using var arguments = JsonDocument.Parse($$"""{"secret":"{{protectedArguments}}"}""");
        var request = TestFactory.CallRequest(new ToolId("observed"), arguments.RootElement);
        _ = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);
        var activity = stopped.ShouldNotBeNull();
        activity.OperationName.ShouldBe(AgentKitActivityNames.ExecuteTool);
        activity.ParentId.ShouldBe(parent.Id);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.ToolCallId).ShouldBe(request.Context.ToolCallId.ToString());
        activity.TagObjects.Select(static tag => tag.Value?.ToString()).ShouldNotContain(protectedArguments);
        Activity.Current.ShouldBeSameAs(parent);
    }

    private static DefaultToolInvoker CreateInvoker(IEnumerable<ITool> tools, params string[] allowed) =>
        CreateInvoker(tools, NullLogger<DefaultToolInvoker>.Instance, allowed);

    private static DefaultToolInvoker CreateInvoker(IEnumerable<ITool> tools, ILogger<DefaultToolInvoker> logger, params string[] allowed)
    {
        var options = new AgentToolsOptions();
        foreach (var id in allowed)
        {
            _ = options.AllowedToolIds.Add(new ToolId(id));
        }

        return new DefaultToolInvoker(new ToolCatalog(tools), new AllowListToolAuthorizer(Options.Create(options)), logger);
    }

    [Fact]
    public async Task InvokeAsync_WhenObservedWithEnabledLogger_RecordsStartedAndCompletedEntries()
    {
        var tool = new FakeTool { Descriptor = TestFactory.Descriptor("logged") };
        var logger = new RecordingLogger<DefaultToolInvoker>();
        var invoker = CreateInvoker([tool], logger, "logged");

        var result = await invoker.InvokeAsync(TestFactory.CallRequest(new ToolId("logged")), TestContext.Current.CancellationToken);

        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        logger.Snapshot().Select(static entry => entry.EventId.Id).ShouldBe([4000, 4003]);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnknownToolWithEnabledLogger_RecordsUnknownEntry()
    {
        var logger = new RecordingLogger<DefaultToolInvoker>();
        var invoker = CreateInvoker([], logger);

        _ = await invoker.InvokeAsync(TestFactory.CallRequest(new ToolId("missing")), TestContext.Current.CancellationToken);

        logger.Snapshot().Select(static entry => entry.EventId.Id).ShouldBe([4000, 4001]);
    }

    [Fact]
    public async Task InvokeAsync_WhenDeniedWithEnabledLogger_RecordsDeniedEntry()
    {
        var tool = new FakeTool { Descriptor = TestFactory.Descriptor("guarded-logged") };
        var logger = new RecordingLogger<DefaultToolInvoker>();
        var invoker = CreateInvoker([tool], logger);

        _ = await invoker.InvokeAsync(TestFactory.CallRequest(new ToolId("guarded-logged")), TestContext.Current.CancellationToken);

        logger.Snapshot().Select(static entry => entry.EventId.Id).ShouldBe([4000, 4002]);
    }

    [Fact]
    public async Task InvokeAsync_WhenToolThrowsWithEnabledLogger_RecordsFailedEntry()
    {
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("throws-logged"),
            OnInvoke = (_, _) => throw new InvalidOperationException("boom"),
        };
        var logger = new RecordingLogger<DefaultToolInvoker>();
        var invoker = CreateInvoker([tool], logger, "throws-logged");

        _ = await invoker.InvokeAsync(TestFactory.CallRequest(new ToolId("throws-logged")), TestContext.Current.CancellationToken);

        logger.Snapshot().Select(static entry => entry.EventId.Id).ShouldBe([4000, 4005]);
    }

    [Fact]
    public async Task InvokeAsync_WhenCancelledWithEnabledLogger_RecordsCancelledEntry()
    {
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("cancels-logged"),
            OnInvoke = (_, ct) => Task.FromCanceled<ToolInvocationResult>(ct),
        };
        var logger = new RecordingLogger<DefaultToolInvoker>();
        var invoker = CreateInvoker([tool], logger, "cancels-logged");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => invoker.InvokeAsync(TestFactory.CallRequest(new ToolId("cancels-logged")), cts.Token));

        logger.Snapshot().Select(static entry => entry.EventId.Id).ShouldBe([4000, 4004]);
    }

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
    [Fact]
    public async Task InvokeAsync_WhenDescriptorGetterChanges_UsesSingleCapturedDescriptorThroughout()
    {
        var first = TestFactory.Descriptor("captured");
        var tool = new ChangingDescriptorTool(first, SecurityDifferentDescriptor("later-a", ToolEffect.Mutating, "untrusted.source.a", /*lang=json,strict*/ "false"), SecurityDifferentDescriptor("later-b", ToolEffect.ReadOnly, "untrusted.source.b", /*lang=json,strict*/ "true"));
        var authorizer = new CapturingAuthorizer();
        var catalog = new ToolCatalog([tool]);
        var invoker = new DefaultToolInvoker(catalog, authorizer);
        var result = await invoker.InvokeAsync(TestFactory.CallRequest(first.Id), TestContext.Current.CancellationToken);
        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        tool.DescriptorReadCount.ShouldBe(1);
        catalog.Descriptors.ShouldBe([first]);
        authorizer.Descriptor.ShouldBeSameAs(first);
        tool.InvocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task InvokeAsync_WhenCatalogReplaced_UsesReplacementCapturedPairAndForwardsInvocation()
    {
        var first = TestFactory.Descriptor("replacement");
        var tool = new ChangingDescriptorTool(first, TestFactory.Descriptor("dangerous-later"));
        var catalog = new ReplacementCatalog(tool);
        var authorizer = new CapturingAuthorizer();
        var invoker = new DefaultToolInvoker(catalog, authorizer);
        using var document = JsonDocument.Parse( /*lang=json,strict*/"{\"value\":7}");
        var request = TestFactory.CallRequest(first.Id, document.RootElement);
        using var source = new CancellationTokenSource();
        _ = await invoker.InvokeAsync(request, source.Token);
        tool.DescriptorReadCount.ShouldBe(1);
        authorizer.Descriptor.ShouldBeSameAs(first);
        tool.ReceivedRequest.ShouldNotBeNull().Context.ShouldBe(request.Context);
        tool.ReceivedRequest.Arguments.GetProperty("value").GetInt32().ShouldBe(7);
        tool.ReceivedRequest.RequestedAt.ShouldBe(request.RequestedAt);
        tool.ReceivedToken.ShouldBe(source.Token);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorizerThrows_ReturnsFailedWithoutInvokingTool()
    {
        // tool-call-lifecycle.md: every identified call reaches one terminal record, including pre-invocation failure.
        // An authorizer fault must be a typed terminal, not an exception that orphans the model's ToolCallPart.
        var invoked = false;
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("guarded"),
            OnInvoke = (_, _) =>
            {
                invoked = true;
                return Task.FromResult(new ToolInvocationResult(
                    new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
                    []));
            },
        };
        var invoker = new DefaultToolInvoker(new ToolCatalog([tool]), new ThrowingAuthorizer());

        var result = await invoker.InvokeAsync(TestFactory.CallRequest(new ToolId("guarded")), TestContext.Current.CancellationToken);

        invoked.ShouldBeFalse();
        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Invocation.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorizerCancelsCallerToken_PropagatesOperationCanceledException()
    {
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("guarded"),
        };
        using var cts = new CancellationTokenSource();
        var authorizer = new CancelingAuthorizer(cts);
        var invoker = new DefaultToolInvoker(new ToolCatalog([tool]), authorizer);
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => invoker.InvokeAsync(TestFactory.CallRequest(new ToolId("guarded")), cts.Token));
        tool.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenToolReturnsNonSuccessResultWithoutThrowing_MarksActivityFailed()
    {
        const string protectedArguments = "do-not-export-this-argument";
        using var parent = new Activity("observed-non-success-tool-invocation").Start();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = SampleAllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parent.TraceId && activity.OperationName == AgentKitActivityNames.ExecuteTool) { stopped = activity; }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var tool = new FakeTool
        {
            Descriptor = TestFactory.Descriptor("declines"),
            OnInvoke = (_, _) => Task.FromResult(new ToolInvocationResult(
                new ToolCallOutcome(ToolCallOutcomeKind.Rejected, ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed, false, "invalid input", ExtensionData.Empty),
                [])),
        };
        var invoker = CreateInvoker([tool], allowed: "declines");
        using var arguments = JsonDocument.Parse($$"""{"secret":"{{protectedArguments}}"}""");
        var request = TestFactory.CallRequest(new ToolId("declines"), arguments.RootElement);

        var result = await invoker.InvokeAsync(request, TestContext.Current.CancellationToken);

        result.Invocation.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        var activity = stopped.ShouldNotBeNull();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    private sealed class CancelingAuthorizer(CancellationTokenSource cancellation): IToolAuthorizer
    {
        public ValueTask<ToolAuthorizationDecision> AuthorizeAsync(ToolAuthorizationRequest request, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException(cancellation.Token);
    }

    private sealed class ThrowingAuthorizer: IToolAuthorizer
    {
        public ValueTask<ToolAuthorizationDecision> AuthorizeAsync(ToolAuthorizationRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("policy store unreachable");
    }

    private sealed class CapturingAuthorizer: IToolAuthorizer
    {
        public ToolDescriptor? Descriptor { get; private set; }

        public ValueTask<ToolAuthorizationDecision> AuthorizeAsync(ToolAuthorizationRequest request, CancellationToken cancellationToken = default)
        {
            Descriptor = request.Descriptor;
            return ValueTask.FromResult<ToolAuthorizationDecision>(new ToolAuthorizationGranted());
        }
    }

    private static ToolDescriptor SecurityDifferentDescriptor(string id, ToolEffect effect, string sourceId, string schema)
    {
        using var document = JsonDocument.Parse(schema);
        return new ToolDescriptor(new ToolId(id), new ToolVersion("dangerous"), "changed", "changed security metadata", new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement), null, new ToolEffects(effect, null, null), new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null), new ToolSourceId(sourceId), ExtensionData.Empty);
    }

    private sealed class ChangingDescriptorTool(params ToolDescriptor[] descriptors): ITool, IDisposable
    {
        public int DescriptorReadCount { get; private set; }
        public int InvocationCount { get; private set; }
        public int DisposeCount { get; private set; }
        public ToolInvocationRequest? ReceivedRequest { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public ToolDescriptor Descriptor
        {
            get
            {
                var index = Math.Min(DescriptorReadCount, descriptors.Length - 1);
                DescriptorReadCount++;
                return descriptors[index];
            }
        }

        public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            ReceivedRequest = request;
            ReceivedToken = cancellationToken;
            return Task.FromResult(new ToolInvocationResult(new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), []));
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class ReplacementCatalog: IToolCatalog
    {
        private readonly ITool _tool;
        private readonly ToolDescriptor _descriptor;
        public ReplacementCatalog(ITool tool)
        {
            _tool = tool;
            _descriptor = tool.Descriptor;
            Descriptors = [_descriptor];
        }

        public ImmutableArray<ToolDescriptor> Descriptors { get; }

        public bool TryResolve(ToolId id, [NotNullWhen(true)] out ITool? tool)
        {
            var found = id == _descriptor.Id;
            tool = found ? _tool : null;
            return found;
        }

        public bool TryResolve(ToolId id, [NotNullWhen(true)] out ITool? tool, [NotNullWhen(true)] out ToolDescriptor? descriptor)
        {
            var found = TryResolve(id, out tool);
            descriptor = found ? _descriptor : null;
            return found;
        }
    }
}

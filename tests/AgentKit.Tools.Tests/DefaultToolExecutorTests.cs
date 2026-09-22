// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Collections.Immutable;
using System.Text.Json;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies <see cref="DefaultToolExecutor"/> sequential pipeline behavior.</summary>
public sealed class DefaultToolExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenAliasUnknown_ReturnsUnknownToolTerminalResult()
    {
        var executor = CreateExecutor(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult())));

        var request = CallRequest(new ToolAlias("missing-alias"));
        var result = await executor.ExecuteAsync(capture, [request], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results.Length.ShouldBe(1);
        result.Results[0].Status.ShouldBe(ToolTerminalStatus.UnknownTool);
        result.Results[0].ToolId.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenArgumentsInvalid_ReturnsInvalidArgumentsWithoutInvoking()
    {
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        var executor = CreateExecutor(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker, RequiredPathSchema());

        var request = CallRequest(new ToolAlias("read"), "{}u8"u8.ToArray());
        var result = await executor.ExecuteAsync(capture, [request], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.InvalidArguments);
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizationDenied_ReturnsDeniedWithoutInvoking()
    {
        var invoker = new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult()));
        var executor = CreateExecutor(new DenyInvocationAuthority());
        await using var capture = await CreateCaptureAsync(invoker);

        var result = await executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.Denied);
        invoker.Invocations.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvokerThrows_ReturnsInvocationFailedTerminalResult()
    {
        var executor = CreateExecutor(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker(static async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        }));

        var result = await executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.InvocationFailed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelled_PropagatesCancellation()
    {
        var executor = CreateExecutor(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            return SuccessResult();
        }));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthorizedAndInvokerSucceeds_ReturnsSucceededTerminalResult()
    {
        var executor = CreateExecutor(new AllowInvocationAuthority());
        await using var capture = await CreateCaptureAsync(new RecordingInvoker((_, _) => ValueTask.FromResult(SuccessResult("ok"))));

        var result = await executor.ExecuteAsync(capture, [CallRequest()], ExecutionCapability(), TestContext.Current.CancellationToken);

        result.Results[0].Status.ShouldBe(ToolTerminalStatus.Succeeded);
        _ = result.Results[0].Acceptance.ShouldNotBeNull();
        var text = result.Results[0].Content.ShouldHaveSingleItem().ShouldBeOfType<ToolResultTextContent>();
        text.Text.ShouldBe("ok");
    }

    private static DefaultToolExecutor CreateExecutor(ISecurityAuthority authority)
    {
        var limits = new ToolSchemaLimits(262_144, 64, 10_000, 100_000);
        var schemaEngine = new BoundedToolSchemaEngine(
            TimeProvider.System,
            NullLogger<BoundedToolSchemaEngine>.Instance,
            NullLogger<CompiledToolSchema>.Instance);
        return new DefaultToolExecutor(
            new ToolCallResolver(TimeProvider.System, NullLogger<ToolCallResolver>.Instance),
            new ToolArgumentValidator(schemaEngine, TimeProvider.System),
            new FixedSecurityAuthoritySelector(authority),
            new FixedSecurityRequestIdGenerator(),
            new ToolResultNormalizer(),
            limits,
            TimeProvider.System,
            NullLogger<DefaultToolExecutor>.Instance);
    }

    private static async Task<ToolCatalogCapture> CreateCaptureAsync(RecordingInvoker invoker, ToolDescriptor? descriptor = null)
    {
        var tool = descriptor ?? ToolCaptureTestData.Descriptor();
        var publication = ToolCaptureTestData.Snapshot([tool]);
        var bindings = ToolCaptureTestData.Bindings(tool, invoker);
        var provider = new StaticToolProvider(
            new ToolProviderBindings(publication, bindings),
            TimeProvider.System,
            NullLogger<StaticToolProvider>.Instance,
            NullLogger<ToolProviderCapture>.Instance);
        var providerCapture = await provider.DiscoverAsync(ToolCaptureTestData.Discovery(), TestContext.Current.CancellationToken);
        var alias = new ToolAlias("read");
        var identity = new ToolIdentity(tool.Id, tool.Version);
        var discovery = ToolCaptureTestData.Discovery();
        var snapshot = new ToolCatalogSnapshot(
            discovery.AgentId,
            discovery.SessionId,
            discovery.RunId,
            discovery.Identity,
            discovery.Authorization.PolicySnapshot,
            discovery.Authorization.AgentDefinitionRevision,
            discovery.Authorization.ConfigurationVersion,
            new ToolCatalogVersion("catalog-1"),
            ImmutableDictionary.Create<ToolSourceId, ToolSourceVersion>().Add(publication.SourceId, publication.SourceVersion),
            [tool],
            ImmutableDictionary.Create<ToolIdentity, ToolExecutionPolicyReference>().Add(
                identity,
                new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1))),
            ImmutableDictionary.Create<ToolAlias, ToolIdentity>().Add(alias, identity));
        return new ToolCatalogCapture(
            snapshot,
            ImmutableDictionary.Create<ToolSourceId, IToolProviderCapture>().Add(publication.SourceId, providerCapture),
            TimeProvider.System,
            NullLogger<ToolCatalogCapture>.Instance);
    }

    private static ToolCallRequest CallRequest(ToolAlias? alias = null, byte[]? rawArguments = null)
    {
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var sessionId = new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var runId = new RunId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var turnId = new TurnId(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        var operationId = new OperationId(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(operationId, runId, turnId);
        var authorization = TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);
        return new ToolCallRequest(
            agentId,
            sessionId,
            runId,
            turnId,
            operationId,
            new ToolCallId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
            authorization,
            new ToolCatalogVersion("catalog-1"),
            sourceOrdinal: 0,
            alias ?? new ToolAlias("read"),
            rawArguments is null ? [123, 125] : [.. rawArguments],
            DateTimeOffset.UnixEpoch);
    }

    private static ToolDescriptor RequiredPathSchema()
    {
        using var document = JsonDocument.Parse("""{"type":"object","required":["path"],"properties":{"path":{"type":"string"}}}""");
        var baseDescriptor = ToolCaptureTestData.Descriptor();
        return new ToolDescriptor(
            baseDescriptor.Id,
            baseDescriptor.Version,
            baseDescriptor.Name,
            baseDescriptor.Description,
            new JsonSchema(baseDescriptor.InputSchema.Dialect, document.RootElement),
            baseDescriptor.OutputSchema,
            baseDescriptor.Effects,
            baseDescriptor.ExecutionHints,
            baseDescriptor.SourceId,
            baseDescriptor.Extensions);
    }

    private static ToolExecutionCapability ExecutionCapability() => new(
        new SessionExecutionCapability(
            TestSecurityEvidence.SessionProfile(),
            new UnsupportedSessionCoordinator(),
            new UnsupportedSessionRunCoordinator()),
        new BudgetExecutionCapability(
            new BudgetProfileKey("standard"),
            new BudgetProfileVersion(1),
            TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                new RunId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
                new TurnId(Guid.Parse("55555555-5555-5555-5555-555555555555"))),
            new FakeBudgetScope()));

    private static ToolInvocationResult SuccessResult(string text = "") => new(
        new ToolCallOutcome(
            ToolCallOutcomeKind.Success,
            ToolTerminalStatus.Succeeded,
            SideEffectCertainty.DefinitelyPerformed,
            false,
            null,
            ExtensionData.Empty),
        string.IsNullOrEmpty(text) ? [] : [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)]);

    private sealed class RecordingInvoker(Func<ToolInvocationContext, CancellationToken, ValueTask<ToolInvocationResult>> handler): IToolInvoker
    {
        public int Invocations { get; private set; }

        public async ValueTask<ToolInvocationResult> InvokeAsync(ToolInvocationContext context, CancellationToken cancellationToken = default)
        {
            Invocations++;
            return await handler(context, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class FixedSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
    {
        public SecurityRequestId Create() => new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    }

    private sealed class AllowInvocationAuthority: ISecurityAuthority
    {
        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            var authorization = request.Authorization
                ?? throw new InvalidOperationException("Tests must supply authorization on security requests.");
            var grant = new SecurityGrant(
                new GrantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
                request.Id,
                request.Scope,
                request.Identity,
                authorization,
                request.Audience,
                request.Kind,
                request.Effect,
                request.Resources,
                request.InputFingerprint,
                authorization.PolicySnapshot.Version,
                new SecurityRevocationVersion(1),
                DateTimeOffset.UnixEpoch,
                request.Deadline,
                request.RequestedUses);
            return ValueTask.FromResult<SecurityDecision>(
                new SecurityAllowed(request.Id, authorization.PolicySnapshot.Version, grant));
        }
    }

    private sealed class DenyInvocationAuthority: ISecurityAuthority
    {
        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var authorization = request.Authorization!;
            return ValueTask.FromResult<SecurityDecision>(
                new SecurityDenied(
                    request.Id,
                    authorization.PolicySnapshot.Version,
                    new SecurityDenial("test.denied", "Denied by test authority.")));
        }
    }

    private sealed class FakeBudgetScope: IBudgetScope
    {
        public BudgetScopeId Id { get; } = new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        public BudgetScopeAddress Address { get; } = new(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new RunId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            new OperationId(Guid.Parse("44444444-4444-4444-4444-444444444444")));

        public ValueTask<BudgetReservationResult> ReserveAsync(BudgetReservationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(ImmutableArray<BudgetReservationRequest> requests, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

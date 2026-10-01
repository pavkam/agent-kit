// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Text.Json;

using AgentKit.TestSupport;

/// <summary>Verifies that <see cref="ToolRunCatalogCaptureFactory"/> applies a per-run tool allow-list to the captured catalog.</summary>
public sealed class ToolRunCatalogCaptureFactoryTests
{
    private static readonly ToolSourceId SourceId = new("tests");

    private static ToolDescriptor Descriptor(string id)
    {
        using var schema = JsonDocument.Parse("{}");
        return new ToolDescriptor(
            new ToolId(id),
            new ToolVersion("1"),
            id,
            "A test tool.",
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), schema.RootElement.Clone()),
            null,
            new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            SourceId,
            ExtensionData.Empty);
    }

    private static ToolIdentity Identity(ToolDescriptor descriptor) => new(descriptor.Id, descriptor.Version);

    private static RunToolCatalogCaptureRequest Request()
    {
        var operation = ProviderEgressHarness.Operation;
        var runId = new RunId(Guid.Parse("c0000000-0000-0000-0000-000000000003"));
        var correlation = new InRunOperationCorrelation(
            new OperationId(Guid.Parse("c0000000-0000-0000-0000-000000000004")),
            runId,
            new TurnId(Guid.Parse("c0000000-0000-0000-0000-000000000005")));
        return new RunToolCatalogCaptureRequest(
            operation.AgentId,
            operation.SessionId!.Value,
            runId,
            TestSecurityEvidence.Authorization(operation.AgentId, operation.SessionId.Value, correlation, operation.Identity));
    }

    private static InnerCapture Inner(RunToolCatalogCaptureRequest request)
    {
        var read = Descriptor("read");
        var write = Descriptor("write");
        var policy = new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("p"), new ToolExecutionPolicyVersion(1));
        return new InnerCapture(new ToolCatalogSnapshot(
            request.AgentId,
            request.SessionId,
            request.RunId,
            request.Authorization.Identity,
            request.Authorization.PolicySnapshot,
            request.Authorization.AgentDefinitionRevision,
            request.Authorization.ConfigurationVersion,
            new ToolCatalogVersion("catalog"),
            ImmutableDictionary<ToolSourceId, ToolSourceVersion>.Empty.Add(SourceId, new ToolSourceVersion("1")),
            [read, write],
            ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference>.Empty.Add(Identity(read), policy).Add(Identity(write), policy),
            ImmutableDictionary<ToolAlias, ToolIdentity>.Empty.Add(new ToolAlias("read"), Identity(read)).Add(new ToolAlias("write"), Identity(write))));
    }

    [Fact]
    public async Task CreateAsync_WhenNoAllowListIsGiven_ReturnsTheCapturedCatalogUnchanged()
    {
        var request = Request();
        var inner = Inner(request);
        var factory = new ToolRunCatalogCaptureFactory(new FixedCatalog(inner));

        var capture = await factory.CreateAsync(request, TestContext.Current.CancellationToken);

        capture.ShouldBeSameAs(inner);
        capture.Snapshot.Tools.Length.ShouldBe(2);
    }

    [Fact]
    public async Task CreateAsync_WhenAnAllowListIsGiven_ExposesOnlyTheIntersectionAndOwnsTheInnerCapture()
    {
        var request = Request() with { AllowedTools = [new ToolId("read"), new ToolId("missing")] };
        var inner = Inner(request);
        var factory = new ToolRunCatalogCaptureFactory(new FixedCatalog(inner));

        var capture = await factory.CreateAsync(request, TestContext.Current.CancellationToken);

        _ = capture.ShouldBeOfType<AllowListedToolCatalogCapture>();
        capture.Snapshot.Tools.Select(static tool => tool.Id).ShouldBe([new ToolId("read")]);
        await capture.DisposeAsync();
        inner.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_WhenTheAllowListIsEmpty_ExposesNoTool()
    {
        var request = Request() with { AllowedTools = [] };
        var factory = new ToolRunCatalogCaptureFactory(new FixedCatalog(Inner(request)));

        var capture = await factory.CreateAsync(request, TestContext.Current.CancellationToken);

        capture.Snapshot.Tools.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var factory = new ToolRunCatalogCaptureFactory(new FixedCatalog(Inner(Request())));

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await factory.CreateAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    private sealed class FixedCatalog(InnerCapture capture): IToolCatalog
    {
        public ValueTask<IToolCatalogCapture> CaptureAsync(ToolDiscoveryRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IToolCatalogCapture>(capture);
    }

    private sealed class InnerCapture(ToolCatalogSnapshot snapshot): IToolCatalogCapture
    {
        public int Disposals { get; private set; }

        public ToolCatalogSnapshot Snapshot { get; } = snapshot;

        public ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(ToolIdentity identity, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerUnavailable(identity, "inner"));

        public ValueTask DisposeAsync()
        {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}

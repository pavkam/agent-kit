// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Text.Json;

/// <summary>Verifies that <see cref="AllowListedToolCatalogCapture"/> fails closed outside a per-run tool allow-list.</summary>
public sealed class AllowListedToolCatalogCaptureTests
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

    private static (RecordingCapture Inner, ToolDescriptor Read, ToolDescriptor Write) Create()
    {
        var read = Descriptor("read");
        var write = Descriptor("write");
        var authorization = TestSupport.ProviderEgressHarness.Operation.Authorization;
        var snapshot = new ToolCatalogSnapshot(
            TestSupport.ProviderEgressHarness.Operation.AgentId,
            TestSupport.ProviderEgressHarness.Operation.SessionId!.Value,
            new RunId(Guid.Parse("c0000000-0000-0000-0000-000000000001")),
            authorization.Identity,
            authorization.PolicySnapshot,
            authorization.AgentDefinitionRevision,
            authorization.ConfigurationVersion,
            new ToolCatalogVersion("catalog"),
            ImmutableDictionary<ToolSourceId, ToolSourceVersion>.Empty.Add(SourceId, new ToolSourceVersion("1")),
            [read, write],
            ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference>.Empty
                .Add(Identity(read), new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("p"), new ToolExecutionPolicyVersion(1)))
                .Add(Identity(write), new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("p"), new ToolExecutionPolicyVersion(1))),
            ImmutableDictionary<ToolAlias, ToolIdentity>.Empty
                .Add(new ToolAlias("read"), Identity(read))
                .Add(new ToolAlias("write"), Identity(write)));
        return (new RecordingCapture(snapshot), read, write);
    }

    [Fact]
    public void Snapshot_WhenListNamesOneTool_ExposesOnlyThatToolAndItsAlias()
    {
        var (inner, read, _) = Create();

        var capture = new AllowListedToolCatalogCapture(inner, [read.Id]);

        capture.Snapshot.Tools.ShouldBe([read]);
        capture.Snapshot.ProviderAliases.Keys.ShouldBe([new ToolAlias("read")]);
        capture.Snapshot.Version.ShouldBe(inner.Snapshot.Version);
    }

    [Fact]
    public async Task AcquireInvokerAsync_WhenIdentityIsAllowed_DelegatesToTheInnerCapture()
    {
        var (inner, read, _) = Create();
        var capture = new AllowListedToolCatalogCapture(inner, [read.Id]);

        var result = await capture.AcquireInvokerAsync(Identity(read), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolInvokerUnavailable>();
        inner.Acquisitions.ShouldBe([Identity(read)]);
    }

    [Fact]
    public async Task AcquireInvokerAsync_WhenIdentityIsOutsideTheList_IsUnavailableWithoutTouchingTheInnerCapture()
    {
        var (inner, read, write) = Create();
        var capture = new AllowListedToolCatalogCapture(inner, [read.Id]);

        var result = await capture.AcquireInvokerAsync(Identity(write), TestContext.Current.CancellationToken);

        var unavailable = result.ShouldBeOfType<ToolInvokerUnavailable>();
        unavailable.Identity.ShouldBe(Identity(write));
        inner.Acquisitions.ShouldBeEmpty();
    }

    [Fact]
    public async Task AcquireInvokerAsync_WhenListIsEmpty_RefusesEveryTool()
    {
        var (inner, read, write) = Create();
        var capture = new AllowListedToolCatalogCapture(inner, []);

        _ = await capture.AcquireInvokerAsync(Identity(read), TestContext.Current.CancellationToken);
        _ = await capture.AcquireInvokerAsync(Identity(write), TestContext.Current.CancellationToken);

        capture.Snapshot.Tools.ShouldBeEmpty();
        inner.Acquisitions.ShouldBeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalled_DisposesTheInnerCapture()
    {
        var (inner, read, _) = Create();
        var capture = new AllowListedToolCatalogCapture(inner, [read.Id]);

        await capture.DisposeAsync();

        inner.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Constructor_WhenArgumentIsInvalid_ThrowsTheExactException()
    {
        var (inner, _, _) = Create();

        Should.Throw<ArgumentNullException>(() => new AllowListedToolCatalogCapture(null!, [])).ParamName.ShouldBe("inner");
        Should.Throw<ArgumentException>(() => new AllowListedToolCatalogCapture(inner, default)).ParamName.ShouldBe("allowedTools");
    }

    [Fact]
    public async Task AcquireInvokerAsync_WhenIdentityIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var (inner, read, _) = Create();
        var capture = new AllowListedToolCatalogCapture(inner, [read.Id]);

        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await capture.AcquireInvokerAsync(default, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("identity");
    }

    private sealed class RecordingCapture(ToolCatalogSnapshot snapshot): IToolCatalogCapture
    {
        public List<ToolIdentity> Acquisitions { get; } = [];

        public int Disposals { get; private set; }

        public ToolCatalogSnapshot Snapshot { get; } = snapshot;

        public ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(ToolIdentity identity, CancellationToken cancellationToken = default)
        {
            Acquisitions.Add(identity);
            return ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerUnavailable(identity, "inner"));
        }

        public ValueTask DisposeAsync()
        {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}

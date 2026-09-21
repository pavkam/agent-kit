// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

using System.Collections.Immutable;
using System.Text.Json;

/// <summary>Builds deterministic catalog captures for loop tests.</summary>
internal sealed class FakeToolRunCatalogCaptureFactory: IToolRunCatalogCaptureFactory
{
    /// <inheritdoc/>
    public IToolCatalogCapture Create(RunToolCatalogCaptureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var schema = JsonDocument.Parse("{}");
        var descriptor = new ToolDescriptor(
            new ToolId("test-tool"),
            new ToolVersion("1"),
            "test",
            "A test tool.",
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), schema.RootElement),
            null,
            new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId("tests"),
            ExtensionData.Empty);
        var identity = new ToolIdentity(descriptor.Id, descriptor.Version);
        var snapshot = new ToolCatalogSnapshot(
            request.AgentId,
            request.SessionId,
            request.RunId,
            request.Authorization.Identity,
            request.Authorization.PolicySnapshot,
            request.Authorization.AgentDefinitionRevision,
            request.Authorization.ConfigurationVersion,
            new ToolCatalogVersion("test-catalog"),
            ImmutableDictionary<ToolSourceId, ToolSourceVersion>.Empty.Add(new ToolSourceId("tests"), new ToolSourceVersion("1")),
            [descriptor],
            ImmutableDictionary<ToolIdentity, ToolExecutionPolicyReference>.Empty.Add(
                identity,
                new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("test"), new ToolExecutionPolicyVersion(1))),
            ImmutableDictionary<ToolAlias, ToolIdentity>.Empty.Add(new ToolAlias("test-tool"), identity));
        return new FakeToolCatalogCapture(snapshot);
    }

    private sealed class FakeToolCatalogCapture(ToolCatalogSnapshot snapshot): IToolCatalogCapture
    {
        public ToolCatalogSnapshot Snapshot { get; } = snapshot;

        public ValueTask<ToolInvokerLeaseResult> AcquireInvokerAsync(ToolIdentity identity, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerUnavailable(identity, "Test capture does not acquire leases."));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

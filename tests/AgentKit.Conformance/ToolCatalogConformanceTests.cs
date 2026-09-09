// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using System.Text.Json;

/// <summary>Defines reusable coherent descriptor-capture requirements for tool catalogs.</summary>
/// <typeparam name="TFixture">The implementation fixture used to compose a catalog.</typeparam>
public abstract class ToolCatalogConformanceTests<TFixture>
    where TFixture : IToolCatalogConformanceFixture, new()
{
    /// <summary>Verifies paired resolution returns the borrowed tool and the exact descriptor advertised at capture.</summary>
    [Fact]
    public void TryResolve_WhenDescriptorGetterChanges_ReturnsSingleCapturedPair()
    {
        var captured = Descriptor("captured");
        var tool = new ChangingTool(captured, Descriptor("later"));
        var catalog = new TFixture().Create([tool]);

        var found = catalog.TryResolve(captured.Id, out var resolved, out var descriptor);

        found.ShouldBeTrue();
        resolved.ShouldBeSameAs(tool);
        descriptor.ShouldBeSameAs(captured);
        catalog.Descriptors.ShouldBe([captured]);
        tool.DescriptorReadCount.ShouldBe(1);
    }

    /// <summary>Verifies unsuccessful paired resolution returns neither half of a stale binding.</summary>
    [Fact]
    public void TryResolve_WhenIdentityUnknown_ReturnsNoCapturedPair()
    {
        var catalog = new TFixture().Create([new ChangingTool(Descriptor("known"))]);

        var found = catalog.TryResolve(new ToolId("unknown"), out var tool, out var descriptor);

        found.ShouldBeFalse();
        tool.ShouldBeNull();
        descriptor.ShouldBeNull();
    }

    private static ToolDescriptor Descriptor(string id)
    {
        using var document = JsonDocument.Parse("{}");
        return new ToolDescriptor(
            new ToolId(id), new ToolVersion("1"), id, "description",
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement),
            null, new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId("conformance"), ExtensionData.Empty);
    }

    private sealed class ChangingTool(params ToolDescriptor[] descriptors): ITool
    {
        public int DescriptorReadCount { get; private set; }

        public ToolDescriptor Descriptor => descriptors[Math.Min(DescriptorReadCount++, descriptors.Length - 1)];

        public Task<ToolInvocationResult> InvokeAsync(
            ToolInvocationRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

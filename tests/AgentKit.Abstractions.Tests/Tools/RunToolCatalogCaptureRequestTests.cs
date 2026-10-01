// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="RunToolCatalogCaptureRequest"/> validation and its per-run tool allow-list.</summary>
public sealed class RunToolCatalogCaptureRequestTests
{
    private static RunToolCatalogCaptureRequest Create() =>
        new(
            ProviderEgressHarness.Operation.AgentId,
            ProviderEgressHarness.Operation.SessionId!.Value,
            new RunId(Guid.Parse("c0000000-0000-0000-0000-000000000002")),
            ProviderEgressHarness.Operation.Authorization);

    [Fact]
    public void AllowedTools_WhenOmitted_IsNullSoTheWholeCatalogIsExposed() =>
        Create().AllowedTools.ShouldBeNull();

    [Fact]
    public void AllowedTools_WhenSet_RoundTripsIncludingAnEmptyList()
    {
        (Create() with { AllowedTools = [new ToolId("read")] }).AllowedTools.ShouldBe([new ToolId("read")]);
        (Create() with { AllowedTools = [] }).AllowedTools.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public void AllowedTools_WhenDefaultArray_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => Create() with { AllowedTools = default(ImmutableArray<ToolId>) });

        exception.ParamName.ShouldBe("AllowedTools");
    }

    [Fact]
    public void Constructor_WhenAgentIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var operation = ProviderEgressHarness.Operation;

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new RunToolCatalogCaptureRequest(default, operation.SessionId!.Value, new RunId(Guid.NewGuid()), operation.Authorization));

        exception.ParamName.ShouldBe("agentId");
    }
}

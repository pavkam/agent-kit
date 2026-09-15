// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

using AgentKit;

/// <summary>Verifies ContextReady behavior and contracts.</summary>
public sealed class ContextReadyTests
{
    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ContextReady(null!));

        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenContextIsNullWithRepairs_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ContextReady(null!, []));

        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenRepairsIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ContextReady(CreateContext(), default));

        exception.ParamName.ShouldBe("repairs");
    }

    [Fact]
    public void Constructor_WhenRepairsContainNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ContextReady(CreateContext(), [null!]));

        exception.ParamName.ShouldBe("repairs");
    }

    [Fact]
    public void Constructor_WhenOnlyContextSupplied_ExposesEmptyRepairs()
    {
        var context = CreateContext();

        var ready = new ContextReady(context);

        ready.Context.ShouldBeSameAs(context);
        ready.Repairs.IsDefault.ShouldBeFalse();
        ready.Repairs.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenRepairsSupplied_PreservesOrder()
    {
        var first = Repair(1);
        var second = Repair(2);

        var ready = new ContextReady(CreateContext(), [first, second]);

        ready.Repairs.ShouldBe([first, second]);
    }

    [Fact]
    public void With_WhenContextIsNull_ThrowsArgumentNullException()
    {
        var ready = new ContextReady(CreateContext());

        var exception = Should.Throw<ArgumentNullException>(() => ready with { Context = null! });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenRepairsIsDefault_ThrowsArgumentException()
    {
        var ready = new ContextReady(CreateContext());

        var exception = Should.Throw<ArgumentException>(() => ready with { Repairs = default });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Equals_WhenRepairsMatchByContent_InstancesAreEqual()
    {
        var context = CreateContext();

        var first = new ContextReady(context, [Repair(1)]);
        var second = new ContextReady(context, [Repair(1)]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenRepairsDiffer_InstancesAreNotEqual()
    {
        var context = CreateContext();

        new ContextReady(context, [Repair(1)]).ShouldNotBe(new ContextReady(context, [Repair(2)]));
    }

    private static HistoryRepair Repair(int value) => new(
        [new MessageId(Guid.Parse($"00000000-0000-0000-0000-{value:D12}"))],
        HistoryRepairKind.ExcludedIncompleteMessage,
        "excluded",
        ExtensionData.Empty);

    private static LlmRequestContext CreateContext()
    {
        var capabilities = new ModelCapabilities(supportsSystemInstructions: true, supportsStreaming: true, supportsToolCalls: true, supportsParallelToolCalls: true, supportsStructuredOutput: true, supportsReasoning: true, supportsVisionInput: true, ExtensionData.Empty);
        var model = new ModelDescriptor(new ModelAlias("chat"), new ProviderId("test-provider"), new ApiFamilyId("test-api"), new ModelId("test-model"), deploymentId: null, capabilities, new ModelLimits(maxContextTokens: 4096, maxOutputTokens: 1024), pricing: null, ExtensionData.Empty);
        return new LlmRequestContext(new ModelRequestId(Guid.Parse("dc591d0d-5ae3-47ee-8255-e1357764fc0e")), model, messages: [], tools: [], LlmToolChoice.Auto, LlmRequestSettings.Default, ExtensionData.Empty);
    }
}

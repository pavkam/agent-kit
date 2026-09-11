// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

public sealed class ToolSchemaProcessorTests
{
    [Fact]
    public void Constructor_WhenLimitsNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => new ToolSchemaProcessor(null!, TestContext.Current.CancellationToken)).ParamName.ShouldBe("limits");

    [Fact]
    public void Preflight_WhenInvalidOrCancelled_PropagatesExactFailure()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var processor = new ToolSchemaProcessor(ToolSchemaTestData.Limits, cancellation.Token);
        Should.Throw<ArgumentNullException>(() => processor.Preflight(null!)).ParamName.ShouldBe("schema");
        Should.Throw<OperationCanceledException>(() => processor.Preflight(ToolSchemaTestData.Schema("{}"))).CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public void Validate_WhenInvalidOrCancelled_PropagatesExactFailure()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var processor = new ToolSchemaProcessor(ToolSchemaTestData.Limits, cancellation.Token);
        var schema = ToolSchemaTestData.Schema("{}"); var instance = ToolSchemaTestData.Instance("{}");
        Should.Throw<ArgumentNullException>(() => processor.Validate(null!, instance)).ParamName.ShouldBe("schema");
        Should.Throw<ArgumentException>(() => processor.Validate(schema, default)).ParamName.ShouldBe("instance");
        Should.Throw<OperationCanceledException>(() => processor.Validate(schema, instance)).CancellationToken.ShouldBe(cancellation.Token);
    }
}

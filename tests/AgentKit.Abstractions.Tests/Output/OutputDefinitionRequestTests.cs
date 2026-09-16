// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Output;

/// <summary>Verifies OutputDefinitionRequest behavior and contracts.</summary>
public sealed class OutputDefinitionRequestTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var id = new OutputDefinitionId("output");
        var version = new OutputDefinitionVersion("1");
        var request = new OutputDefinitionRequest(id, version);
        request.Id.ShouldBe(id);
        request.Version.ShouldBe(version);
    }

    [Fact]
    public void Constructor_WhenVersionIsNull_AllowsLatest()
    {
        var request = new OutputDefinitionRequest(new OutputDefinitionId("output"), null);
        request.Version.ShouldBeNull();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new OutputDefinitionRequest(new OutputDefinitionId("output"), new OutputDefinitionVersion("1"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}

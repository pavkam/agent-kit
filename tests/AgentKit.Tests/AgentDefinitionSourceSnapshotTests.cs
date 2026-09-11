// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;
/// <summary>Verifies AgentDefinitionSourceSnapshot behavior and contracts.</summary>
public sealed class AgentDefinitionSourceSnapshotTests
{
    [Fact]
    public void AgentDefinitionSourceSnapshot_WhenSourceIdIsDefault_ThrowsBeforeConstruction()
    {
        var exception = Should.Throw<ArgumentException>(() => new AgentDefinitionSourceSnapshot(default, new AgentDefinitionSourceVersion(1), 0, []));
        exception.ParamName.ShouldBe("sourceId");
    }

    [Fact]
    public void AgentDefinitionSourceSnapshot_WhenValuesMatch_IsStructurallyEqual()
    {
        var definition = CompositionTestData.Definition();
        var first = new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("source"), new AgentDefinitionSourceVersion(1), 2, [definition]);
        var second = new AgentDefinitionSourceSnapshot(new AgentDefinitionSourceId("source"), new AgentDefinitionSourceVersion(1), 2, [definition]);
        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }
}

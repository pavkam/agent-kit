// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentIOComponentDefaults behavior and contracts.</summary>
public sealed class AgentIOComponentDefaultsTests
{
    [Fact]
    public void InputCoordinatorKey_WhenRead_MatchesInputCoordinatorKeyValue() =>
        AgentIOComponentDefaults.InputCoordinatorKey.ShouldBe(new ComponentKey<IInputCoordinator>(AgentIOComponentDefaults.InputCoordinatorKeyValue));

    [Fact]
    public void OutputPublisherKey_WhenRead_MatchesOutputPublisherKeyValue() =>
        AgentIOComponentDefaults.OutputPublisherKey.ShouldBe(new ComponentKey<IOutputPublisher>(AgentIOComponentDefaults.OutputPublisherKeyValue));
}

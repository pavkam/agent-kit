// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentProviderComponentDefaults behavior and contracts.</summary>
public sealed class AgentProviderComponentDefaultsTests
{
    [Fact]
    public void ModelSelectorKey_WhenRead_MatchesModelSelectorKeyValue() =>
        AgentProviderComponentDefaults.ModelSelectorKey.ShouldBe(new ComponentKey<IModelSelector>(AgentProviderComponentDefaults.ModelSelectorKeyValue));

    [Fact]
    public void ModelExecutorKey_WhenRead_MatchesModelExecutorKeyValue() =>
        AgentProviderComponentDefaults.ModelExecutorKey.ShouldBe(new ComponentKey<IModelRequestExecutor>(AgentProviderComponentDefaults.ModelExecutorKeyValue));

    [Fact]
    public void Keys_WhenRead_AreDistinctNonblankValues()
    {
        AgentProviderComponentDefaults.ModelSelectorKeyValue.ShouldNotBeNullOrWhiteSpace();
        AgentProviderComponentDefaults.ModelExecutorKeyValue.ShouldNotBeNullOrWhiteSpace();
        AgentProviderComponentDefaults.ModelSelectorKeyValue.ShouldNotBe(AgentProviderComponentDefaults.ModelExecutorKeyValue);
    }
}

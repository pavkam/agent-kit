// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using System.Text.Json;

using AgentKit;

/// <summary>Verifies ConfigurationSemanticValue behavior and contracts.</summary>
public sealed class ConfigurationSemanticValueTests
{
    [Fact]
    public void TypedValues_Constructor_WhenReferenceNull_ThrowsExactException()
    {
        Should.Throw<ArgumentNullException>(() => new SecurityProfileConfigurationValue(null!)).ParamName.ShouldBe("publication");
        Should.Throw<ArgumentNullException>(() => new SessionProfileConfigurationValue(null!)).ParamName.ShouldBe("profile");
        Should.Throw<ArgumentNullException>(() => new ModelSelectionConfigurationValue(null!)).ParamName.ShouldBe("policy");
        Should.Throw<ArgumentNullException>(() => new ToolsetConfigurationValue(null!)).ParamName.ShouldBe("publication");
    }

    [Fact]
    public void SessionAndToolsetValues_Constructor_WhenValid_RetainExactTypedReferences()
    {
        var session = new SessionProfileReference(new SessionProfileKey("session"), new SessionProfileVersion(1));
        var toolset = new ToolsetPublication(new ToolsetKey("tools"), new ToolsetVersion(1), new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("policy"), new ToolExecutionPolicyVersion(1)), [], []);
        new SessionProfileConfigurationValue(session).Profile.ShouldBe(session);
        new ToolsetConfigurationValue(toolset).Publication.ShouldBe(toolset);
    }

    [Fact]
    public void ConfigurationSemanticValue_CopyConstructor_WhenExternalVariantBootstrapsFromBuiltIn_ThrowsExactException()
    {
        using var document = JsonDocument.Parse("true");
        var original = new ConfigurationJsonValue(document.RootElement);
        var exception = Should.Throw<ArgumentException>(() => new ForeignConfigurationValue(original));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void ConfigurationSemanticValue_CopyConstructor_WhenOriginalNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ForeignConfigurationValue(null!));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void ConfigurationSemanticValue_CopyConstructor_WhenBuiltInVariantCopies_PreservesValue()
    {
        using var document = JsonDocument.Parse("true");
        var original = new ConfigurationJsonValue(document.RootElement);
        var copy = original with
        {
        };
        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(ConfigurationJsonValue));
    }

    private sealed record ForeignConfigurationValue(ConfigurationSemanticValue Original): ConfigurationSemanticValue(Original);
}

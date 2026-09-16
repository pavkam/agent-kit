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
    public void ModelSelectionValue_Constructor_WhenValid_RetainsExactPolicy()
    {
        var policy = new ModelSelectionPolicy([new ModelAlias("chat")]);
        new ModelSelectionConfigurationValue(policy).Policy.ShouldBe(policy);
    }

    [Fact]
    public void SecurityProfileValue_Constructor_WhenValid_RetainsExactPublication()
    {
        var publication = new SecurityProfilePublication(new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-000000000004")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"));
        new SecurityProfileConfigurationValue(publication).Publication.ShouldBe(publication);
    }

    [Fact]
    public void TypedValues_With_WhenApplied_ProducesEqualCopies()
    {
        var policy = new ModelSelectionPolicy([new ModelAlias("chat")]);
        var publication = new SecurityProfilePublication(new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")), new AgentDefinitionRevision(1), new ConfigurationVersion(1), new SecurityProfileKey("security"), new SecurityProfileVersion(1), new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(Guid.Parse("d0000000-0000-0000-0000-000000000004")), new SecurityPolicyVersion(1), new ContentHash("sha256:policy")), new ComponentKey<ISecurityAuthority>("authority"));
        var session = new SessionProfileReference(new SessionProfileKey("session"), new SessionProfileVersion(1));
        var toolset = new ToolsetPublication(new ToolsetKey("tools"), new ToolsetVersion(1), new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("policy"), new ToolExecutionPolicyVersion(1)), [], []);

        var modelCopy = new ModelSelectionConfigurationValue(policy) with { };
        var securityCopy = new SecurityProfileConfigurationValue(publication) with { };
        var sessionCopy = new SessionProfileConfigurationValue(session) with { };
        var toolsetCopy = new ToolsetConfigurationValue(toolset) with { };

        modelCopy.ShouldBe(new ModelSelectionConfigurationValue(policy));
        securityCopy.ShouldBe(new SecurityProfileConfigurationValue(publication));
        sessionCopy.ShouldBe(new SessionProfileConfigurationValue(session));
        toolsetCopy.ShouldBe(new ToolsetConfigurationValue(toolset));
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

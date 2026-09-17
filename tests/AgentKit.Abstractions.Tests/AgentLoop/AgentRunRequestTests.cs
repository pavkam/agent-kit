// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunRequest behavior and contracts.</summary>
public sealed class AgentRunRequestTests
{
    [Fact]
    public void Constructor_WhenIdentityIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, null!,
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), Policy(), ModelRequirements.None, [], [],
            LlmToolChoice.Auto, LlmRequestSettings.Default, 8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("identity");

    [Fact]
    public void Constructor_WhenAuthorizationIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            null!, LoopTestData.SessionProfile(), Policy(), ModelRequirements.None, [], [],
            LlmToolChoice.Auto, LlmRequestSettings.Default, 8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("authorization");

    [Fact]
    public void Constructor_WhenAuthorizationScopeAgentDiffers_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new AgentRunRequest(
            new AgentId(Guid.NewGuid()), LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), Policy(), ModelRequirements.None, [], [],
            LlmToolChoice.Auto, LlmRequestSettings.Default, 8, TimeSpan.FromMinutes(1), ExtensionData.Empty));
        exception.ParamName.ShouldBe("authorization");
    }

    [Fact]
    public void Constructor_WhenSessionProfileIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), null!, Policy(), ModelRequirements.None, [], [],
            LlmToolChoice.Auto, LlmRequestSettings.Default, 8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("sessionProfile");

    [Fact]
    public void Constructor_WhenModelPolicyIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), null!, ModelRequirements.None, [], [],
            LlmToolChoice.Auto, LlmRequestSettings.Default, 8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("modelPolicy");

    [Fact]
    public void Constructor_WhenModelRequirementsIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), Policy(), null!, [], [],
            LlmToolChoice.Auto, LlmRequestSettings.Default, 8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("modelRequirements");

    [Fact]
    public void Constructor_WhenInstructionsIsDefault_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), Policy(), ModelRequirements.None, default, [],
            LlmToolChoice.Auto, LlmRequestSettings.Default, 8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("instructions");

    [Fact]
    public void Constructor_WhenToolsIsDefault_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), Policy(), ModelRequirements.None, [], default,
            LlmToolChoice.Auto, LlmRequestSettings.Default, 8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("tools");

    [Fact]
    public void Constructor_WhenToolChoiceIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), Policy(), ModelRequirements.None, [], [],
            null!, LlmRequestSettings.Default, 8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("toolChoice");

    [Fact]
    public void Constructor_WhenSettingsIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), Policy(), ModelRequirements.None, [], [],
            LlmToolChoice.Auto, null!, 8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("settings");

    [Fact]
    public void Constructor_WhenMaxTurnsIsNotPositive_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), Policy(), ModelRequirements.None, [], [],
            LlmToolChoice.Auto, LlmRequestSettings.Default, 0, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("maxTurns");

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsNotPositive_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), Policy(), ModelRequirements.None, [], [],
            LlmToolChoice.Auto, LlmRequestSettings.Default, 8, TimeSpan.Zero, ExtensionData.Empty)).ParamName.ShouldBe("attemptTimeout");

    [Fact]
    public void Constructor_WhenExtensionsIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunRequest(
            LoopTestData.AgentId, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), Policy(), ModelRequirements.None, [], [],
            LlmToolChoice.Auto, LlmRequestSettings.Default, 8, TimeSpan.FromMinutes(1), null!)).ParamName.ShouldBe("extensions");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = LoopTestData.RunRequest();
        request.AgentId.ShouldBe(LoopTestData.AgentId);
        request.Agent.ShouldBeNull();
        request.SessionId.ShouldBe(LoopTestData.SessionId);
        request.BranchId.ShouldBe(LoopTestData.BranchId);
        request.RunId.ShouldBe(LoopTestData.RunId);
        request.Configuration.ShouldBeNull();
        request.SessionProfile.ShouldBe(LoopTestData.SessionProfile());
        request.ModelPolicy.ShouldBe(new ModelSelectionPolicy([new ModelAlias("chat")]));
        request.ModelRequirements.ShouldBe(ModelRequirements.None);
        request.Instructions.ShouldBeEmpty();
        request.Tools.ShouldBeEmpty();
        request.ToolChoice.ShouldBe(LlmToolChoice.Auto);
        request.Settings.ShouldBe(LlmRequestSettings.Default);
        request.MaxTurns.ShouldBe(8);
        request.AttemptTimeout.ShouldBe(TimeSpan.FromMinutes(1));
        request.Extensions.ShouldBe(ExtensionData.Empty);
        request.Observer.ShouldBeNull();
    }

    [Fact]
    public void Equality_WhenInstructionsAndToolsArePresent_HashesEveryElement()
    {
        var instruction = LoopTestData.AssistantMessage();
        var tool = new LlmToolDefinition(new ToolId("t"), "tool", null, default);
        var first = LoopTestData.RunRequest() with { Instructions = [instruction], Tools = [tool] };
        var second = LoopTestData.RunRequest() with { Instructions = [instruction], Tools = [tool] };
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenOtherIsNull_IsNotEqual() => LoopTestData.RunRequest().Equals(null).ShouldBeFalse();

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = LoopTestData.RunRequest();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ModelSelectionPolicy Policy() => new([new ModelAlias("chat")]);

    [Fact]
    public void SecondConstructor_WhenAgentIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunRequest(
            null!, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), LoopTestData.Configuration(),
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("agent");

    [Fact]
    public void SecondConstructor_WhenConfigurationIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunRequest(
            LoopTestData.Definition(), LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), null!,
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty)).ParamName.ShouldBe("configuration");

    [Fact]
    public void SecondConstructor_WhenAgentRevisionDiffersFromAuthorization_ThrowsExactArgumentException()
    {
        var agent = LoopTestData.Definition() with { Revision = new AgentDefinitionRevision(2) };
        var exception = Should.Throw<ArgumentException>(() => new AgentRunRequest(
            agent, LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), LoopTestData.Configuration(),
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty));
        exception.ParamName.ShouldBe("agent");
    }

    [Fact]
    public void SecondConstructor_WhenConfigurationVersionDiffersFromAuthorization_ThrowsExactArgumentException()
    {
        var configuration = new EffectiveConfigurationSnapshot(new ConfigurationVersion(2), new ContentHash("sha256:test-session-profile"), [], []);
        _ = Should.Throw<ArgumentException>(() => new AgentRunRequest(
            LoopTestData.Definition(), LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), configuration,
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty));
    }

    [Fact]
    public void SecondConstructor_WhenConfigurationFingerprintDiffersFromSessionProfile_ThrowsExactArgumentException()
    {
        var configuration = new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("sha256:other"), [], []);
        _ = Should.Throw<ArgumentException>(() => new AgentRunRequest(
            LoopTestData.Definition(), LoopTestData.SessionId, LoopTestData.BranchId, LoopTestData.RunId, LoopTestData.Identity(),
            LoopTestData.RunAuthorization(), LoopTestData.SessionProfile(), configuration,
            8, TimeSpan.FromMinutes(1), ExtensionData.Empty));
    }

    [Fact]
    public void SecondConstructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = LoopTestData.RunRequestFromDefinition();
        request.Agent.ShouldBe(LoopTestData.Definition());
        request.AgentId.ShouldBe(LoopTestData.AgentId);
        request.Configuration.ShouldBe(LoopTestData.Configuration());
    }

    [Fact]
    public void SecondConstructor_With_WhenApplied_ProducesEqualCopy()
    {
        var original = LoopTestData.RunRequestFromDefinition();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenInstructionsIsSetToDefault_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequest();
        var action = () => request with { Instructions = default };
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("Instructions");
    }

    [Fact]
    public void With_WhenToolsIsSetToDefault_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequest();
        var action = () => request with { Tools = default };
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("Tools");
    }

    [Fact]
    public void With_WhenMaxTurnsIsSetToZero_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequest();
        var action = () => request with { MaxTurns = 0 };
        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("MaxTurns");
    }

    [Fact]
    public void With_WhenAttemptTimeoutIsSetToZero_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequest();
        var action = () => request with { AttemptTimeout = TimeSpan.Zero };
        action.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("AttemptTimeout");
    }

    [Fact]
    public void With_WhenExtensionsIsSetToNull_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequest();
        var action = () => request with { Extensions = null! };
        action.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("Extensions");
    }

    [Fact]
    public void SecondConstructor_With_WhenModelPolicyDivergesFromThePinnedAgent_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequestFromDefinition();
        var action = () => request with { ModelPolicy = new ModelSelectionPolicy([new ModelAlias("poisoned")]) };
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("ModelPolicy");
    }

    [Fact]
    public void SecondConstructor_With_WhenInstructionsDivergesFromThePinnedAgent_ThrowsBeforeConstruction()
    {
        var request = LoopTestData.RunRequestFromDefinition();
        var poisoned = new SystemMessage(
            new MessageId(Guid.Parse("c0000000-0000-0000-0000-000000000001")),
            LoopTestData.AgentId, LoopTestData.SessionId, null, LoopTestData.BranchId, null, null,
            DateTimeOffset.UnixEpoch, MessageState.Complete,
            [new TextPart("poisoned", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var action = () => request with { Instructions = [poisoned] };
        action.ShouldThrow<ArgumentException>().ParamName.ShouldBe("Instructions");
    }
}

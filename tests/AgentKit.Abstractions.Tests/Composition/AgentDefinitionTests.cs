// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;



/// <summary>Verifies AgentDefinition behavior and contracts.</summary>
public sealed class AgentDefinitionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AgentDefinition_WhenSelectedProfileIsBlank_ThrowsWithExactParameterName(bool securityProfile)
    {
        var security = securityProfile ? default : new SecurityProfileKey("security");
        var session = securityProfile ? new SessionProfileKey("session") : default;
        var exception = Should.Throw<ArgumentException>(() => Definition(security, session));
        exception.ParamName.ShouldBe(securityProfile ? "securityProfile" : "sessionProfile");
    }

    [Fact]
    public void AgentDefinition_WhenProfilesAreExplicit_PreservesSelections()
    {
        var security = new SecurityProfileKey("security");
        var session = new SessionProfileKey("session");
        var definition = Definition(security, session);
        definition.SecurityProfile.ShouldBe(security);
        definition.SessionProfile.ShouldBe(session);
    }

    private static AgentDefinition Definition(SecurityProfileKey securityProfile, SessionProfileKey sessionProfile) => new(new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")), new AgentDefinitionRevision(1), "agent", new ModelSelectionPolicy([new ModelAlias("chat")]), ModelRequirements.None, [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)), ExtensionData.Empty, securityProfile, sessionProfile);
}

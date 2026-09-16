// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

internal static class CompositionTestData
{
    public static AgentId AgentId => new(Guid.Parse("a0000000-0000-0000-0000-000000000001"));

    public static AgentDefinition Definition(SecurityProfileKey? securityProfile = null, SessionProfileKey? sessionProfile = null) => new(
        AgentId,
        new AgentDefinitionRevision(1),
        "agent",
        new ModelSelectionPolicy([new ModelAlias("chat")]),
        ModelRequirements.None,
        [],
        [],
        LlmToolChoice.Auto,
        LlmRequestSettings.Default,
        new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)),
        ExtensionData.Empty,
        securityProfile ?? new SecurityProfileKey("security"),
        sessionProfile ?? new SessionProfileKey("session"));

    public static CompositionDiagnostic Diagnostic() => new("code", "message");
}

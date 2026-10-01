// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

internal static class CompositionTestData
{
    public static AgentId AgentId => new(Guid.Parse("a0000000-0000-0000-0000-000000000001"));

    public static AgentDefinition Definition(SecurityProfileKey? securityProfile = null, SessionProfileKey? sessionProfile = null) =>
        TestSupport.AgentDefinitionFixtures.Create(AgentId, displayName: "agent", securityProfile: securityProfile, sessionProfile: sessionProfile);

    public static CompositionDiagnostic Diagnostic() => new("code", "message");
}

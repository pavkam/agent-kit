// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

/// <summary>Verifies deterministic durable-session path composition.</summary>
[Collection(EnvironmentVariableTestGroup.Name)]
public sealed class AgentRuntimeTests
{
    /// <summary>Verifies the default database is isolated outside the writable workspace.</summary>
    [Fact]
    public void SessionDatabasePath_WhenOverrideIsAbsent_ReturnsWorkspaceKeyedApplicationDataPath()
    {
        var previous = Environment.GetEnvironmentVariable("CODING_AGENT_SESSION_DB");
        try
        {
            Environment.SetEnvironmentVariable("CODING_AGENT_SESSION_DB", null);
            var workspace = Path.Combine(Path.GetTempPath(), "coding-agent-workspace");

            var result = AgentRuntime.SessionDatabasePath(Path.GetFullPath(workspace));

            result.ShouldStartWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            result.ShouldEndWith("sessions.db");
            result.ShouldNotContain(Path.Combine(Path.GetFullPath(workspace), ".agentkit"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODING_AGENT_SESSION_DB", previous);
        }
    }

    /// <summary>Verifies shared storage cannot collapse distinct workspaces into one agent identity.</summary>
    [Fact]
    public void AgentIdForWorkspace_WhenWorkspacesDiffer_ReturnsStableIsolatedIdentities()
    {
        var first = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "coding-agent-a"));
        var second = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "coding-agent-b"));

        var firstIdentity = AgentRuntime.AgentIdForWorkspace(first);
        var repeated = AgentRuntime.AgentIdForWorkspace(first + Path.DirectorySeparatorChar);
        var secondIdentity = AgentRuntime.AgentIdForWorkspace(second);

        repeated.ShouldBe(firstIdentity);
        secondIdentity.ShouldNotBe(firstIdentity);
    }

    /// <summary>Verifies an explicit absolute target replaces the application-data default.</summary>
    [Fact]
    public void SessionDatabasePath_WhenAbsoluteOverrideIsPresent_ReturnsOverride()
    {
        var previous = Environment.GetEnvironmentVariable("CODING_AGENT_SESSION_DB");
        var configured = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "coding-agent-override.db"));
        try
        {
            Environment.SetEnvironmentVariable("CODING_AGENT_SESSION_DB", configured);

            var result = AgentRuntime.SessionDatabasePath(Path.GetFullPath(Path.GetTempPath()));

            result.ShouldBe(configured);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODING_AGENT_SESSION_DB", previous);
        }
    }
}

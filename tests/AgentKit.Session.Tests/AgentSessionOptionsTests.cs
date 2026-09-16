// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

/// <summary>Verifies AgentSessionOptions behavior and contracts.</summary>
public sealed class AgentSessionOptionsTests
{
    [Fact]
    public void Properties_WhenDefaulted_MatchDocumentedValues()
    {
        var options = new AgentSessionOptions();

        options.MaximumAppendEntries.ShouldBe(128);
        options.MaximumPageSize.ShouldBe(256);
        options.SecurityRequestLifetime.ShouldBe(TimeSpan.FromMinutes(1));
        options.BusyBehavior.ShouldBe(SessionBusyBehavior.Reject);
        options.BusyWaitTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Properties_WhenSet_RetainAssignedValues()
    {
        var options = new AgentSessionOptions
        {
            MaximumAppendEntries = 4,
            MaximumPageSize = 8,
            SecurityRequestLifetime = TimeSpan.FromSeconds(5),
            BusyBehavior = SessionBusyBehavior.Wait,
            BusyWaitTimeout = TimeSpan.FromSeconds(1),
        };

        options.MaximumAppendEntries.ShouldBe(4);
        options.MaximumPageSize.ShouldBe(8);
        options.SecurityRequestLifetime.ShouldBe(TimeSpan.FromSeconds(5));
        options.BusyBehavior.ShouldBe(SessionBusyBehavior.Wait);
        options.BusyWaitTimeout.ShouldBe(TimeSpan.FromSeconds(1));
    }
}

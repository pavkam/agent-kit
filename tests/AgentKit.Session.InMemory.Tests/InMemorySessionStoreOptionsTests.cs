// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Verifies the documented defaults of <see cref="InMemorySessionStoreOptions"/>.</summary>
public sealed class InMemorySessionStoreOptionsTests
{
    [Fact]
    public void MaximumIssuedReadSnapshots_WhenDefault_Is4096()
    {
        var options = new InMemorySessionStoreOptions();

        options.MaximumIssuedReadSnapshots.ShouldBe(4096);
    }

    [Fact]
    public void MaximumIssuedReadSnapshots_WhenSet_RoundTrips()
    {
        var options = new InMemorySessionStoreOptions { MaximumIssuedReadSnapshots = 7 };

        options.MaximumIssuedReadSnapshots.ShouldBe(7);
    }
}

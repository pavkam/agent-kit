// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityRejected behavior and contracts.</summary>
public sealed class IdentityRejectedTests
{
    [Fact]
    public void IdentityRejected_WhenCreated_PreservesTypedFailure()
    {
        var failure = new IdentityFailure(IdentityFailureKind.Expired, "Authentication evidence expired.", new IdentityIssuerId("issuer"));
        var result = new IdentityRejected(failure);
        result.Failure.ShouldBeSameAs(failure);
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="RetentionPolicy"/> defaults and value semantics.</summary>
public sealed class RetentionPolicyTests
{
    [Fact]
    public void Constructor_WhenOmitted_RetainsUntilExplicitDeletionWithoutExpiry()
    {
        var policy = new RetentionPolicy();

        policy.ExpiresAt.ShouldBeNull();
        policy.RetainUntilExplicitDeletion.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenExpiryIsSupplied_PreservesIt()
    {
        var expiry = DateTimeOffset.UnixEpoch.AddDays(3);

        new RetentionPolicy(expiry, retainUntilExplicitDeletion: false).ExpiresAt.ShouldBe(expiry);
    }

    [Fact]
    public void Equality_WhenPartsMatch_IsStructural() =>
        new RetentionPolicy(DateTimeOffset.UnixEpoch).ShouldBe(new RetentionPolicy(DateTimeOffset.UnixEpoch));
}

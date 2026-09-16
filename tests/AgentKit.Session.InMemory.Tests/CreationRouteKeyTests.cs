// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Verifies CreationRouteKey behavior and contracts.</summary>
public sealed class CreationRouteKeyTests
{
    private static readonly AgentId _agentId = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenTenantIdIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new CreationRouteKey(default, _agentId, new IdempotencyKey("key")));
        exception.ParamName.ShouldBe("tenantId");
    }

    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new CreationRouteKey(new TenantId("tenant"), default, new IdempotencyKey("key")));
        exception.ParamName.ShouldBe("agentId");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new CreationRouteKey(new TenantId("tenant"), _agentId, default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void CreationRouteKey_WhenConstructed_RetainsAllFields()
    {
        var key = new CreationRouteKey(new TenantId("tenant"), _agentId, new IdempotencyKey("create-key"));

        key.TenantId.ShouldBe(new TenantId("tenant"));
        key.AgentId.ShouldBe(_agentId);
        key.IdempotencyKey.ShouldBe(new IdempotencyKey("create-key"));
    }

    [Fact]
    public void CreationRouteKey_WhenFieldsMatch_AreEqual()
    {
        var first = new CreationRouteKey(new TenantId("tenant"), _agentId, new IdempotencyKey("create-key"));
        var second = new CreationRouteKey(new TenantId("tenant"), _agentId, new IdempotencyKey("create-key"));

        first.ShouldBe(second);
    }
}

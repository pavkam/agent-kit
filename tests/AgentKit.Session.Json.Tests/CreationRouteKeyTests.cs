// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the validated key that scopes one logical creation attempt before a session identity exists.</summary>
public sealed class CreationRouteKeyTests
{
    /// <summary>Verifies every field is retained unchanged for valid evidence.</summary>
    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsEveryField()
    {
        var tenantId = new TenantId("tenant-a");
        var agentId = JsonSessionStoreTests.Identifier<AgentId>(1);
        var idempotencyKey = new IdempotencyKey("create-1");

        var key = new CreationRouteKey(tenantId, agentId, idempotencyKey);

        key.TenantId.ShouldBe(tenantId);
        key.AgentId.ShouldBe(agentId);
        key.IdempotencyKey.ShouldBe(idempotencyKey);
    }

    /// <summary>Verifies a blank tenant identity is rejected.</summary>
    [Fact]
    public void Constructor_WhenTenantIdIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new CreationRouteKey(
                default, JsonSessionStoreTests.Identifier<AgentId>(1), new IdempotencyKey("create-1")))
            .ParamName.ShouldBe("tenantId");

    /// <summary>Verifies a default agent identity is rejected.</summary>
    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new CreationRouteKey(
                new TenantId("tenant-a"), default, new IdempotencyKey("create-1")))
            .ParamName.ShouldBe("agentId");

    /// <summary>Verifies a blank idempotency key is rejected.</summary>
    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new CreationRouteKey(
                new TenantId("tenant-a"), JsonSessionStoreTests.Identifier<AgentId>(1), default))
            .ParamName.ShouldBe("idempotencyKey");

    /// <summary>Verifies two keys built from identical evidence compare equal.</summary>
    [Fact]
    public void Equals_WhenAllFieldsMatch_IsEqual()
    {
        var agentId = JsonSessionStoreTests.Identifier<AgentId>(1);
        var left = new CreationRouteKey(new TenantId("tenant-a"), agentId, new IdempotencyKey("create-1"));
        var right = new CreationRouteKey(new TenantId("tenant-a"), agentId, new IdempotencyKey("create-1"));

        left.ShouldBe(right);
    }

    /// <summary>Verifies a differing idempotency key breaks equality.</summary>
    [Fact]
    public void Equals_WhenIdempotencyKeyDiffers_IsNotEqual()
    {
        var agentId = JsonSessionStoreTests.Identifier<AgentId>(1);
        var left = new CreationRouteKey(new TenantId("tenant-a"), agentId, new IdempotencyKey("create-1"));
        var right = new CreationRouteKey(new TenantId("tenant-a"), agentId, new IdempotencyKey("create-2"));

        left.ShouldNotBe(right);
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

/// <summary>Verifies the validated key that scopes one idempotent write to an established session address.</summary>
public sealed class DirectoryWriteKeyTests
{
    /// <summary>Verifies every field is retained unchanged for valid evidence.</summary>
    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsEveryField()
    {
        var tenantId = new TenantId("tenant-a");
        var address = Address();
        var idempotencyKey = new IdempotencyKey("write-1");

        var key = new DirectoryWriteKey(tenantId, address, idempotencyKey);

        key.TenantId.ShouldBe(tenantId);
        key.Address.ShouldBe(address);
        key.IdempotencyKey.ShouldBe(idempotencyKey);
    }

    /// <summary>Verifies a blank tenant identity is rejected.</summary>
    [Fact]
    public void Constructor_WhenTenantIdIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new DirectoryWriteKey(default, Address(), new IdempotencyKey("write-1")))
            .ParamName.ShouldBe("tenantId");

    /// <summary>Verifies a null session address is rejected.</summary>
    [Fact]
    public void Constructor_WhenAddressIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new DirectoryWriteKey(
                new TenantId("tenant-a"), null!, new IdempotencyKey("write-1")))
            .ParamName.ShouldBe("address");

    /// <summary>Verifies a blank idempotency key is rejected.</summary>
    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new DirectoryWriteKey(new TenantId("tenant-a"), Address(), default))
            .ParamName.ShouldBe("idempotencyKey");

    /// <summary>Verifies two keys built from identical evidence compare equal.</summary>
    [Fact]
    public void Equals_WhenAllFieldsMatch_IsEqual()
    {
        var address = Address();
        var left = new DirectoryWriteKey(new TenantId("tenant-a"), address, new IdempotencyKey("write-1"));
        var right = new DirectoryWriteKey(new TenantId("tenant-a"), address, new IdempotencyKey("write-1"));

        left.ShouldBe(right);
    }

    /// <summary>Verifies a differing address breaks equality.</summary>
    [Fact]
    public void Equals_WhenAddressDiffers_IsNotEqual()
    {
        var left = new DirectoryWriteKey(new TenantId("tenant-a"), Address(1), new IdempotencyKey("write-1"));
        var right = new DirectoryWriteKey(new TenantId("tenant-a"), Address(2), new IdempotencyKey("write-1"));

        left.ShouldNotBe(right);
    }

    private static SessionAddress Address(int seed = 1) => new(
        JsonSessionStoreTests.Identifier<AgentId>(seed), JsonSessionStoreTests.Identifier<SessionId>(seed + 1));
}

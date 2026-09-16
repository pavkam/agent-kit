// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

/// <summary>Verifies DirectoryWriteKey behavior and contracts.</summary>
public sealed class DirectoryWriteKeyTests
{
    private static readonly SessionAddress _address = new(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid()));

    [Fact]
    public void Constructor_WhenTenantIdIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new DirectoryWriteKey(default, _address, new IdempotencyKey("key")));
        exception.ParamName.ShouldBe("tenantId");
    }

    [Fact]
    public void Constructor_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new DirectoryWriteKey(new TenantId("tenant"), null!, new IdempotencyKey("key")));
        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public void Constructor_WhenIdempotencyKeyIsBlank_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new DirectoryWriteKey(new TenantId("tenant"), _address, default));
        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public void DirectoryWriteKey_WhenConstructed_RetainsAllFields()
    {
        var key = new DirectoryWriteKey(new TenantId("tenant"), _address, new IdempotencyKey("write-key"));

        key.TenantId.ShouldBe(new TenantId("tenant"));
        key.Address.ShouldBe(_address);
        key.IdempotencyKey.ShouldBe(new IdempotencyKey("write-key"));
    }

    [Fact]
    public void DirectoryWriteKey_WhenFieldsMatch_AreEqual()
    {
        var first = new DirectoryWriteKey(new TenantId("tenant"), _address, new IdempotencyKey("write-key"));
        var second = new DirectoryWriteKey(new TenantId("tenant"), _address, new IdempotencyKey("write-key"));

        first.ShouldBe(second);
    }
}

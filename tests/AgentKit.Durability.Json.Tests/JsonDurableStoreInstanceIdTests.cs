// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json.Tests;

/// <summary>Verifies the store-identity value rejects the absent identity and renders canonically.</summary>
public sealed class JsonDurableStoreInstanceIdTests
{
    /// <summary>Verifies the empty identity is refused, because it would match any unwritten manifest.</summary>
    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsForTheValueArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonDurableStoreInstanceId(Guid.Empty));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a supplied identity is carried unchanged for manifest comparison.</summary>
    [Fact]
    public void Constructor_WhenValueIsSupplied_RetainsItExactly()
    {
        var value = Guid.Parse("2b8c2f9a-0f1e-4b5c-9d3a-6f1e2c4d5a7b");

        var identity = new JsonDurableStoreInstanceId(value);

        identity.Value.ShouldBe(value);
    }

    /// <summary>Verifies rendering stays canonical lowercase, so diagnostics never vary by platform formatting.</summary>
    [Fact]
    public void ToString_WhenCalled_ReturnsTheHyphenatedLowercaseIdentity()
    {
        var identity = new JsonDurableStoreInstanceId(Guid.Parse("2B8C2F9A-0F1E-4B5C-9D3A-6F1E2C4D5A7B"));

        identity.ToString().ShouldBe("2b8c2f9a-0f1e-4b5c-9d3a-6f1e2c4d5a7b");
    }
}

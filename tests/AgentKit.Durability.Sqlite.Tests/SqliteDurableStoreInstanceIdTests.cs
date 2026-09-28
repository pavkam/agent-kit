// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies the store-identity value rejects the one value that cannot identify a store.</summary>
public sealed class SqliteDurableStoreInstanceIdTests
{
    /// <summary>Verifies a nonempty identity is retained exactly.</summary>
    [Fact]
    public void Constructor_WhenValueIsNonEmpty_RetainsTheExactIdentity()
    {
        var value = Guid.NewGuid();

        var identity = new SqliteDurableStoreInstanceId(value);

        identity.Value.ShouldBe(value);
    }

    /// <summary>Verifies the empty GUID is refused, because it cannot distinguish one store from another.</summary>
    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsForTheValueArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new SqliteDurableStoreInstanceId(Guid.Empty));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies diagnostics text is the canonical hyphenated form.</summary>
    [Fact]
    public void ToString_WhenIdentityIsCreated_ReturnsTheCanonicalHyphenatedForm()
    {
        var value = Guid.NewGuid();

        var text = new SqliteDurableStoreInstanceId(value).ToString();

        text.ShouldBe(value.ToString("D"));
    }
}

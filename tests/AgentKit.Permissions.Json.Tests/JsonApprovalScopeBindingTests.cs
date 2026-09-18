// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies JsonApprovalScopeBinding projection, revalidating reconstruction, and JSON round trip.</summary>
public sealed class JsonApprovalScopeBindingTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies a projected binding reconstructs to a domain value equal to the original by domain equality.</summary>
    [Fact]
    public void FromDomainThenToDomain_WhenBindingIsValid_RoundTripsToEqualDomainValue()
    {
        var binding = TestApprovalFactory.CreateBinding(_now);

        var document = JsonApprovalScopeBinding.FromDomain(binding);
        var reconstructed = document.ToDomain();

        reconstructed.ShouldBe(binding);
    }

    /// <summary>Verifies a null domain binding is rejected before projection.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNull()
    {
        var exception = Should.Throw<ArgumentNullException>(static () => JsonApprovalScopeBinding.FromDomain(null!));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a document with a null request is rejected before domain reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenRequestIsNull_ThrowsArgumentNull()
    {
        var document = JsonApprovalScopeBinding.FromDomain(TestApprovalFactory.CreateBinding(_now)) with
        {
            Request = null!,
        };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Request");
    }

    /// <summary>Verifies a document whose approved use count exceeds the captured request is rejected on reconstruction rather than silently widened.</summary>
    [Fact]
    public void ToDomain_WhenAllowedUsesExceedsRequest_ThrowsArgumentOutOfRange()
    {
        var document = JsonApprovalScopeBinding.FromDomain(TestApprovalFactory.CreateBinding(_now)) with
        {
            AllowedUses = 99,
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a binding survives a real JSON encode and decode round trip under the canonical contract.</summary>
    [Fact]
    public void SerializeThenDeserialize_WhenBindingIsValid_RoundTripsExactly()
    {
        var document = JsonApprovalScopeBinding.FromDomain(TestApprovalFactory.CreateBinding(_now));
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, options);
        var decoded = JsonSerializer.Deserialize<JsonApprovalScopeBinding>(bytes, options);

        decoded.ShouldBe(document);
    }
}

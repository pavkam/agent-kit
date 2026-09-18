// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies JsonApprovalRequest projection, revalidating reconstruction, and JSON round trip.</summary>
public sealed class JsonApprovalRequestTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies a projected request reconstructs to a domain value equal to the original by domain equality.</summary>
    [Fact]
    public void FromDomainThenToDomain_WhenRequestIsValid_RoundTripsToEqualDomainValue()
    {
        var request = TestApprovalFactory.CreateRequest(_now);

        var document = JsonApprovalRequest.FromDomain(request);
        var reconstructed = document.ToDomain();

        reconstructed.ShouldBe(request);
    }

    /// <summary>Verifies a null domain request is rejected before projection.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNull()
    {
        var exception = Should.Throw<ArgumentNullException>(static () => JsonApprovalRequest.FromDomain(null!));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a document with a null binding is rejected before domain reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenBindingIsNull_ThrowsArgumentNull()
    {
        var document = JsonApprovalRequest.FromDomain(TestApprovalFactory.CreateRequest(_now)) with
        {
            Binding = null!,
        };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Binding");
    }

    /// <summary>Verifies an empty persisted identity is rejected on reconstruction rather than silently accepted.</summary>
    [Fact]
    public void ToDomain_WhenIdIsEmpty_ThrowsArgumentOutOfRange()
    {
        var document = JsonApprovalRequest.FromDomain(TestApprovalFactory.CreateRequest(_now)) with
        {
            Id = Guid.Empty,
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted presentation is rejected on reconstruction rather than silently accepted.</summary>
    [Fact]
    public void ToDomain_WhenSafePresentationIsBlank_ThrowsArgumentException()
    {
        var document = JsonApprovalRequest.FromDomain(TestApprovalFactory.CreateRequest(_now)) with
        {
            SafePresentation = " ",
        };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a request survives a real JSON encode and decode round trip under the canonical contract.</summary>
    [Fact]
    public void SerializeThenDeserialize_WhenRequestIsValid_RoundTripsExactly()
    {
        var document = JsonApprovalRequest.FromDomain(TestApprovalFactory.CreateRequest(_now));
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, options);
        var decoded = JsonSerializer.Deserialize<JsonApprovalRequest>(bytes, options);

        decoded.ShouldBe(document);
    }
}

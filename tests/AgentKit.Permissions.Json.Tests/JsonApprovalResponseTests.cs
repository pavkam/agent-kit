// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies JsonApprovalResponse projection, revalidating reconstruction, and JSON round trip.</summary>
public sealed class JsonApprovalResponseTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies a projected response reconstructs to a domain value equal to the original by domain equality.</summary>
    [Fact]
    public void FromDomainThenToDomain_WhenResponseIsValid_RoundTripsToEqualDomainValue()
    {
        var request = TestApprovalFactory.CreateRequest(_now);
        var response = TestApprovalFactory.CreateResponse(request);

        var document = JsonApprovalResponse.FromDomain(response);
        var reconstructed = document.ToDomain();

        reconstructed.ShouldBe(response);
    }

    /// <summary>Verifies a projected response carrying a delegated approver identity round-trips exactly.</summary>
    [Fact]
    public void FromDomainThenToDomain_WhenApproverIdentityIsDelegated_RoundTripsToEqualDomainValue()
    {
        var request = TestApprovalFactory.CreateRequest(_now);
        var response = new ApprovalResponse(
            new ApprovalResponseId(Guid.Parse("50000000-0000-0000-0000-00000000000a")),
            request.Id,
            request.Binding,
            ApprovalResolution.Denied,
            TestApprovalFactory.CreateApproverIdentity(_now),
            request.CreatedAt.AddSeconds(1));

        var document = JsonApprovalResponse.FromDomain(response);
        var reconstructed = document.ToDomain();

        reconstructed.ShouldBe(response);
        reconstructed.Resolution.ShouldBe(ApprovalResolution.Denied);
    }

    /// <summary>Verifies a null domain response is rejected before projection.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNull()
    {
        var exception = Should.Throw<ArgumentNullException>(static () => JsonApprovalResponse.FromDomain(null!));

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a document with a null binding is rejected before domain reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenBindingIsNull_ThrowsArgumentNull()
    {
        var request = TestApprovalFactory.CreateRequest(_now);
        var document = JsonApprovalResponse.FromDomain(TestApprovalFactory.CreateResponse(request)) with
        {
            Binding = null!,
        };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Binding");
    }

    /// <summary>Verifies a document with a null approver identity is rejected before domain reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenApproverIdentityIsNull_ThrowsArgumentNull()
    {
        var request = TestApprovalFactory.CreateRequest(_now);
        var document = JsonApprovalResponse.FromDomain(TestApprovalFactory.CreateResponse(request)) with
        {
            ApproverIdentity = null!,
        };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("ApproverIdentity");
    }

    /// <summary>Verifies an undefined persisted resolution is rejected on reconstruction rather than silently accepted.</summary>
    [Fact]
    public void ToDomain_WhenResolutionIsUndefined_ThrowsArgumentOutOfRange()
    {
        var request = TestApprovalFactory.CreateRequest(_now);
        var document = JsonApprovalResponse.FromDomain(TestApprovalFactory.CreateResponse(request)) with
        {
            Resolution = (ApprovalResolution) 99,
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a response survives a real JSON encode and decode round trip under the canonical contract.</summary>
    [Fact]
    public void SerializeThenDeserialize_WhenResponseIsValid_RoundTripsExactly()
    {
        var request = TestApprovalFactory.CreateRequest(_now);
        var document = JsonApprovalResponse.FromDomain(TestApprovalFactory.CreateResponse(request));
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, options);
        var decoded = JsonSerializer.Deserialize<JsonApprovalResponse>(bytes, options);

        decoded.ShouldBe(document);
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies every JsonApprovalLogRecord factory, its argument guards, and its JSON round trip.</summary>
public sealed class JsonApprovalLogRecordTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies a creation record carries the projected request and no response.</summary>
    [Fact]
    public void ForCreation_WhenRequestIsValid_CarriesOnlyRequest()
    {
        var request = TestApprovalFactory.CreateRequest(_now);

        var record = JsonApprovalLogRecord.ForCreation(request);

        record.Kind.ShouldBe(JsonApprovalLogRecordKind.Created);
        record.Request.ShouldBe(JsonApprovalRequest.FromDomain(request));
        record.Response.ShouldBeNull();
    }

    /// <summary>Verifies a null request is rejected before a creation record is built.</summary>
    [Fact]
    public void ForCreation_WhenRequestIsNull_ThrowsArgumentNull()
    {
        var exception = Should.Throw<ArgumentNullException>(
            static () => JsonApprovalLogRecord.ForCreation(null!));

        exception.ParamName.ShouldBe("request");
    }

    /// <summary>Verifies a resolution record carries the projected response and no request.</summary>
    [Fact]
    public void ForResolution_WhenResponseIsValid_CarriesOnlyResponse()
    {
        var request = TestApprovalFactory.CreateRequest(_now);
        var response = TestApprovalFactory.CreateResponse(request);

        var record = JsonApprovalLogRecord.ForResolution(response);

        record.Kind.ShouldBe(JsonApprovalLogRecordKind.Resolved);
        record.Response.ShouldBe(JsonApprovalResponse.FromDomain(response));
        record.Request.ShouldBeNull();
    }

    /// <summary>Verifies a null response is rejected before a resolution record is built.</summary>
    [Fact]
    public void ForResolution_WhenResponseIsNull_ThrowsArgumentNull()
    {
        var exception = Should.Throw<ArgumentNullException>(
            static () => JsonApprovalLogRecord.ForResolution(null!));

        exception.ParamName.ShouldBe("response");
    }

    /// <summary>Verifies a creation record survives a real JSON encode and decode round trip under the canonical contract.</summary>
    [Fact]
    public void SerializeThenDeserialize_WhenCreation_RoundTripsExactly()
    {
        var request = TestApprovalFactory.CreateRequest(
            _now, identity: TestApprovalFactory.CreateApproverIdentity(_now));
        var record = JsonApprovalLogRecord.ForCreation(request);
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, options);
        var decoded = JsonSerializer.Deserialize<JsonApprovalLogRecord>(bytes, options);

        decoded.ShouldBe(record);
    }

    /// <summary>Verifies a resolution record survives a real JSON encode and decode round trip under the canonical contract.</summary>
    [Fact]
    public void SerializeThenDeserialize_WhenResolution_RoundTripsExactly()
    {
        var request = TestApprovalFactory.CreateRequest(_now);
        var response = TestApprovalFactory.CreateResponse(request);
        var record = JsonApprovalLogRecord.ForResolution(response);
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, options);
        var decoded = JsonSerializer.Deserialize<JsonApprovalLogRecord>(bytes, options);

        decoded.ShouldBe(record);
    }
}

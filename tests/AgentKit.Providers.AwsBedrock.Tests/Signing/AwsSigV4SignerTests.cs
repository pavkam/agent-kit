// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.Signing;

/// <summary>
/// Verifies <see cref="AwsSigV4Signer"/> against signature values
/// independently computed with a Python <c>hashlib</c>/<c>hmac</c>
/// reference implementation of the same documented AWS Signature Version 4
/// algorithm, for a fixed, deterministic request.
/// </summary>
public sealed class AwsSigV4SignerTests
{
    private static readonly AwsSigV4Credential Credential = new(
        "AKIDEXAMPLE",
        "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
        sessionToken: null);

    private static readonly DateTimeOffset Timestamp = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SignRequest_WhenGivenKnownInputs_MatchesIndependentlyComputedReferenceSignature()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/anthropic.claude-3-sonnet-20240229-v1%3A0/converse");
        var body = /*lang=json,strict*/ "{\"messages\":[{\"role\":\"user\",\"content\":[{\"text\":\"hi\"}]}]}"u8.ToArray();
        var headers = new Dictionary<string, string> { ["content-type"] = "application/json" };

        var signed = AwsSigV4Signer.SignRequest(
            "POST", uri, headers, body, Credential, "us-east-1", "bedrock", Timestamp);

        signed["x-amz-date"].ShouldBe("20240101T000000Z");
        signed["x-amz-content-sha256"].ShouldBe("c3335e07d6e89650bf94cb98664be69f8774dbc3198df221f113db0a59ea14f0");
        signed["Authorization"].ShouldBe(
            "AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20240101/us-east-1/bedrock/aws4_request, " +
            "SignedHeaders=content-type;host;x-amz-content-sha256;x-amz-date, " +
            "Signature=67167c95cf69ef3b88bc4e6f3a4b420f1eefaa9e7d7e1a5dbf3f573603478ded");
    }

    [Fact]
    public void SignRequest_WhenSessionTokenPresent_IncludesXAmzSecurityTokenHeader()
    {
        var credential = new AwsSigV4Credential("AKIDEXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "session-token-value");
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/foo/converse");
        var headers = new Dictionary<string, string> { ["content-type"] = "application/json" };

        var signed = AwsSigV4Signer.SignRequest(
            "POST", uri, headers, "{}"u8, credential, "us-east-1", "bedrock", Timestamp);

        signed["x-amz-security-token"].ShouldBe("session-token-value");
        signed["Authorization"].ShouldContain("x-amz-security-token");
    }

    [Fact]
    public void SignRequest_WhenNoSessionToken_OmitsXAmzSecurityTokenHeader()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/foo/converse");
        var headers = new Dictionary<string, string> { ["content-type"] = "application/json" };

        var signed = AwsSigV4Signer.SignRequest(
            "POST", uri, headers, "{}"u8, Credential, "us-east-1", "bedrock", Timestamp);

        signed.ContainsKey("x-amz-security-token").ShouldBeFalse();
        signed["Authorization"].ShouldNotContain("x-amz-security-token");
    }

    [Fact]
    public void SignRequest_WhenCalledTwiceWithSameInputs_ProducesIdenticalSignature()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/foo/converse");
        var headers = new Dictionary<string, string> { ["content-type"] = "application/json" };
        var body = /*lang=json,strict*/ "{\"a\":1}"u8.ToArray();

        var first = AwsSigV4Signer.SignRequest("POST", uri, headers, body, Credential, "us-east-1", "bedrock", Timestamp);
        var second = AwsSigV4Signer.SignRequest("POST", uri, headers, body, Credential, "us-east-1", "bedrock", Timestamp);

        first["Authorization"].ShouldBe(second["Authorization"]);
    }
}

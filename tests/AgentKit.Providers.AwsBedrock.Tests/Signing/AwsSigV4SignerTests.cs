// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests.Signing;

using System.Security.Cryptography;

/// <summary>
/// Verifies <see cref="AwsSigV4Signer"/> against signature values derived
/// step by step from the documented AWS Signature Version 4 algorithm for
/// fixed, deterministic requests. The pinned reference signature was
/// additionally confirmed against the AWS SDK for .NET
/// (<c>AWSSDKUtils.CanonicalizeResourcePathV2(..., doubleEncode: true, ...)</c>
/// for the canonical path and <c>AWS4Signer.ComputeSignature</c> for the
/// signature) and an independent Python <c>hashlib</c>/<c>hmac</c>
/// computation.
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
        // The wire path escapes the model ID's colon once; the SigV4 canonical URI for a non-S3
        // service encodes that already-escaped segment again, so "%3A" must be signed as "%253A".
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
            "Signature=73f8b71fcb0c5bd47011139f30192cb059c5ff56739a3c00c075e0fa1a9f6835");
    }

    [Fact]
    public void SignRequest_WhenGivenKnownInputs_MatchesStepByStepSpecificationDerivation()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/anthropic.claude-3-sonnet-20240229-v1%3A0/converse");
        var body = /*lang=json,strict*/ "{\"messages\":[{\"role\":\"user\",\"content\":[{\"text\":\"hi\"}]}]}"u8.ToArray();
        var headers = new Dictionary<string, string> { ["content-type"] = "application/json" };

        var signed = AwsSigV4Signer.SignRequest(
            "POST", uri, headers, body, Credential, "us-east-1", "bedrock", Timestamp);

        var expected = DeriveSignaturePerSpecification(
            canonicalUri: "/model/anthropic.claude-3-sonnet-20240229-v1%253A0/converse",
            host: "bedrock-runtime.us-east-1.amazonaws.com",
            body,
            "bedrock");
        signed["Authorization"].ShouldEndWith($"Signature={expected}");
    }

    [Fact]
    public void SignRequest_WhenPathContainsEncodedColon_SignsDoubleEncodedCanonicalUri()
    {
        // Same request signed with a single-encoded canonical URI must not match: the colon is what
        // every versioned Bedrock model ID carries, so this is the exact divergence that yielded 403s.
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/amazon.titan-text-express-v1%3A0/converse-stream");
        var headers = new Dictionary<string, string> { ["content-type"] = "application/json" };

        var signed = AwsSigV4Signer.SignRequest(
            "POST", uri, headers, "{}"u8, Credential, "us-east-1", "bedrock", Timestamp);

        var doubleEncoded = DeriveSignaturePerSpecification(
            "/model/amazon.titan-text-express-v1%253A0/converse-stream",
            "bedrock-runtime.us-east-1.amazonaws.com",
            "{}"u8.ToArray(),
            "bedrock");
        var singleEncoded = DeriveSignaturePerSpecification(
            "/model/amazon.titan-text-express-v1%3A0/converse-stream",
            "bedrock-runtime.us-east-1.amazonaws.com",
            "{}"u8.ToArray(),
            "bedrock");
        signed["Authorization"].ShouldEndWith($"Signature={doubleEncoded}");
        signed["Authorization"].ShouldNotContain(singleEncoded);
    }

    [Fact]
    public void SignRequest_WhenPathIsArnWithEncodedSlashAndColons_SignsEveryEscapeDoubleEncoded()
    {
        // An inference-profile ARN model ID escaped for the wire carries %3A and %2F; each must be
        // canonicalized to %253A / %252F, and the escaped slash must not be treated as a segment break.
        var uri = new Uri(
            "https://bedrock-runtime.us-east-1.amazonaws.com/model/" +
            "arn%3Aaws%3Abedrock%3Aus-east-1%3A123456789012%3Ainference-profile%2Fus.anthropic.claude-3-5-sonnet-20241022-v2%3A0/converse");
        var headers = new Dictionary<string, string> { ["content-type"] = "application/json" };

        var signed = AwsSigV4Signer.SignRequest(
            "POST", uri, headers, "{}"u8, Credential, "us-east-1", "bedrock", Timestamp);

        var expected = DeriveSignaturePerSpecification(
            "/model/arn%253Aaws%253Abedrock%253Aus-east-1%253A123456789012%253Ainference-profile%252Fus.anthropic.claude-3-5-sonnet-20241022-v2%253A0/converse",
            "bedrock-runtime.us-east-1.amazonaws.com",
            "{}"u8.ToArray(),
            "bedrock");
        signed["Authorization"].ShouldEndWith($"Signature={expected}");
    }

    [Fact]
    public void SignRequest_WhenPathHasOnlyUnreservedCharacters_SignsPathUnchanged()
    {
        // Official aws-c-auth "get-unreserved" vector path: unreserved characters are never encoded,
        // so single and double encoding agree and the canonical URI equals the wire path.
        const string path = "/-._~0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var uri = new Uri("https://example.amazonaws.com" + path);
        var headers = new Dictionary<string, string>();

        var signed = AwsSigV4Signer.SignRequest("GET", uri, headers, [], Credential, "us-east-1", "service", Timestamp);

        var expected = DeriveSignaturePerSpecification(path, "example.amazonaws.com", [], "service", includeContentType: false);
        signed["Authorization"].ShouldEndWith($"Signature={expected}");
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

    /// <summary>Verifies query-string parameters are percent-decoded, re-encoded, and sorted by key then value in the canonical query string.</summary>
    [Fact]
    public void SignRequest_WhenUriHasQueryString_SortsAndEncodesParametersInCanonicalQueryString()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/foo/converse?b=2&a=1&c");
        var headers = new Dictionary<string, string> { ["content-type"] = "application/json" };

        var signed = AwsSigV4Signer.SignRequest(
            "POST", uri, headers, "{}"u8, Credential, "us-east-1", "bedrock", Timestamp);

        var expected = DeriveSignaturePerSpecification(
            "/model/foo/converse",
            "bedrock-runtime.us-east-1.amazonaws.com",
            "{}"u8.ToArray(),
            "bedrock",
            canonicalQueryString: "a=1&b=2&c=");
        signed["Authorization"].ShouldEndWith($"Signature={expected}");
    }

    /// <summary>Verifies a header value with an internal run of multiple spaces is collapsed to one space, per the SigV4 canonical-header trimming rule.</summary>
    [Fact]
    public void SignRequest_WhenHeaderValueHasInternalRunOfSpaces_CollapsesToSingleSpace()
    {
        var uri = new Uri("https://bedrock-runtime.us-east-1.amazonaws.com/model/foo/converse");
        var headers = new Dictionary<string, string> { ["content-type"] = "application/json", ["x-custom"] = "  foo   bar  " };

        var signed = AwsSigV4Signer.SignRequest(
            "POST", uri, headers, "{}"u8, Credential, "us-east-1", "bedrock", Timestamp);

        var expected = DeriveSignaturePerSpecification(
            "/model/foo/converse",
            "bedrock-runtime.us-east-1.amazonaws.com",
            "{}"u8.ToArray(),
            "bedrock",
            extraSignedHeader: ("x-custom", "foo bar"));
        signed["Authorization"].ShouldEndWith($"Signature={expected}");
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

    /// <summary>
    /// Derives the expected signature by following the AWS "Create a signed AWS API request" steps
    /// literally, so the expectation is independent of the signer's own helper methods. The
    /// canonical URI is supplied by the test so the encoding rule under test is stated explicitly.
    /// </summary>
    /// <param name="canonicalUri">The canonical URI line exactly as it must appear in the canonical request.</param>
    /// <param name="host">The host header value.</param>
    /// <param name="body">The request body bytes.</param>
    /// <param name="service">The signing service name in the credential scope.</param>
    /// <param name="includeContentType">Whether a <c>content-type: application/json</c> header is part of the signed request.</param>
    /// <param name="canonicalQueryString">The canonical query string line exactly as it must appear in the canonical request.</param>
    /// <param name="extraSignedHeader">An additional already-normalized header name/value pair to sign, when any.</param>
    /// <returns>The lowercase hexadecimal signature.</returns>
    private static string DeriveSignaturePerSpecification(
        string canonicalUri,
        string host,
        byte[] body,
        string service,
        bool includeContentType = true,
        string canonicalQueryString = "",
        (string Name, string Value)? extraSignedHeader = null)
    {
        // Step 1: canonical request = method \n canonical URI \n canonical query \n canonical headers \n signed headers \n hashed payload.
        var hashedPayload = Convert.ToHexStringLower(SHA256.HashData(body));
        var canonicalHeaders = (includeContentType ? "content-type:application/json\n" : string.Empty) +
                               $"host:{host}\n" +
                               $"x-amz-content-sha256:{hashedPayload}\n" +
                               "x-amz-date:20240101T000000Z\n" +
                               (extraSignedHeader is { } extra ? $"{extra.Name}:{extra.Value}\n" : string.Empty);
        var signedHeaders = (includeContentType ? "content-type;" : string.Empty) + "host;x-amz-content-sha256;x-amz-date" +
                             (extraSignedHeader is { } named ? $";{named.Name}" : string.Empty);
        var canonicalRequest = string.Join(
            '\n',
            includeContentType ? "POST" : "GET",
            canonicalUri,
            canonicalQueryString,
            canonicalHeaders,
            signedHeaders,
            hashedPayload);

        // Step 2: string to sign = algorithm \n request date-time \n credential scope \n Hex(SHA256(canonical request)).
        var credentialScope = $"20240101/us-east-1/{service}/aws4_request";
        var stringToSign = string.Join(
            '\n',
            "AWS4-HMAC-SHA256",
            "20240101T000000Z",
            credentialScope,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        // Step 3: signing key = HMAC(HMAC(HMAC(HMAC("AWS4" + secret, date), region), service), "aws4_request").
        var dateKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes("AWS4" + Credential.SecretAccessKey), Encoding.UTF8.GetBytes("20240101"));
        var dateRegionKey = HMACSHA256.HashData(dateKey, Encoding.UTF8.GetBytes("us-east-1"));
        var dateRegionServiceKey = HMACSHA256.HashData(dateRegionKey, Encoding.UTF8.GetBytes(service));
        var signingKey = HMACSHA256.HashData(dateRegionServiceKey, Encoding.UTF8.GetBytes("aws4_request"));

        // Step 4: signature = Hex(HMAC(signing key, string to sign)).
        return Convert.ToHexStringLower(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign)));
    }
}

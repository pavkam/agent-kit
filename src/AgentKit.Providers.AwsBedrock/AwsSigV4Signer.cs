// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

using System.Globalization;
using System.Security.Cryptography;

/// <summary>
/// Computes an AWS Signature Version 4 <c>Authorization</c> header for one
/// HTTP request, following the canonical-request/string-to-sign/signing-key
/// process AWS documents for manually signing API requests.
/// </summary>
/// <remarks>
/// This signer targets exactly the shape Bedrock Runtime's <c>Converse</c>
/// and <c>ConverseStream</c> operations need: a single, non-chunked HTTPS
/// POST request with a JSON body, signed once. It does not implement the
/// separate chunked, seeded-signature-chain protocol some other AWS
/// streaming services (such as Transcribe's bidirectional audio input) use
/// to sign a sequence of request frames, because Bedrock's request body is
/// ordinary JSON regardless of whether the response is buffered or
/// event-stream framed.
/// </remarks>
internal static class AwsSigV4Signer
{
    private const string _algorithm = "AWS4-HMAC-SHA256";
    private const string _terminator = "aws4_request";
    private const string _amzDateHeader = "x-amz-date";
    private const string _amzContentSha256Header = "x-amz-content-sha256";
    private const string _amzSecurityTokenHeader = "x-amz-security-token";
    private const string _dateTimeFormat = "yyyyMMddTHHmmssZ";
    private const string _dateFormat = "yyyyMMdd";

    /// <summary>
    /// Computes the headers that, added to an HTTP request, authenticate it
    /// with AWS Signature Version 4.
    /// </summary>
    /// <param name="method">The HTTP method, such as <c>POST</c>.</param>
    /// <param name="uri">The absolute request URI, including its path and any query string.</param>
    /// <param name="headers">
    /// The request's own headers to include in the signature (at minimum
    /// <c>host</c>, and <c>content-type</c> when the request has a body),
    /// keyed by header name. This method does not mutate the given
    /// dictionary.
    /// </param>
    /// <param name="body">The raw request body bytes, or an empty span if the request has no body.</param>
    /// <param name="credential">The AWS credential to sign with.</param>
    /// <param name="region">The AWS Region the request targets, such as <c>us-east-1</c>.</param>
    /// <param name="service">The AWS service signing name, such as <c>bedrock</c>.</param>
    /// <param name="timestamp">The instant the request is signed at.</param>
    /// <returns>
    /// The additional headers to attach to the request:
    /// <c>x-amz-date</c>, <c>x-amz-content-sha256</c>, <c>Authorization</c>,
    /// and (when the credential carries one) <c>x-amz-security-token</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="method"/>, <paramref name="uri"/>,
    /// <paramref name="headers"/>, or <paramref name="credential"/> is
    /// null.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="uri"/> is not an absolute URI.</exception>
    public static IReadOnlyDictionary<string, string> SignRequest(
        string method,
        Uri uri,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySpan<byte> body,
        AwsSigV4Credential credential,
        string region,
        string service,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNotAbsoluteUri(uri);

        var amzDate = timestamp.ToString(_dateTimeFormat, CultureInfo.InvariantCulture);
        var dateStamp = timestamp.ToString(_dateFormat, CultureInfo.InvariantCulture);
        var payloadHash = Hex(SHA256.HashData(body));

        var signingHeaders = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = uri.Authority,
            [_amzDateHeader] = amzDate,
            [_amzContentSha256Header] = payloadHash,
        };

        foreach (var (name, value) in headers)
        {
            signingHeaders[name.ToLowerInvariant()] = value;
        }

        if (credential.SessionToken is { Length: > 0 } sessionToken)
        {
            signingHeaders[_amzSecurityTokenHeader] = sessionToken;
        }

        var canonicalUri = BuildCanonicalUri(uri);
        var canonicalQueryString = BuildCanonicalQueryString(uri);
        var signedHeaderNames = string.Join(';', signingHeaders.Keys);
        var canonicalHeaders = string.Concat(signingHeaders.Select(pair => $"{pair.Key}:{Trim(pair.Value)}\n"));

        var canonicalRequest = string.Join(
            '\n',
            method.ToUpperInvariant(),
            canonicalUri,
            canonicalQueryString,
            canonicalHeaders,
            signedHeaderNames,
            payloadHash);

        var credentialScope = $"{dateStamp}/{region}/{service}/{_terminator}";
        var stringToSign = string.Join(
            '\n',
            _algorithm,
            amzDate,
            credentialScope,
            Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        var signingKey = DeriveSigningKey(credential.SecretAccessKey, dateStamp, region, service);
        var signature = Hex(HmacSha256(signingKey, stringToSign));

        var authorizationHeader =
            $"{_algorithm} Credential={credential.AccessKeyId}/{credentialScope}, " +
            $"SignedHeaders={signedHeaderNames}, Signature={signature}";

        var result = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [_amzDateHeader] = amzDate,
            [_amzContentSha256Header] = payloadHash,
            ["Authorization"] = authorizationHeader,
        };

        if (credential.SessionToken is { Length: > 0 } token)
        {
            result[_amzSecurityTokenHeader] = token;
        }

        return result;
    }

    /// <summary>
    /// Builds the canonical URI for a non-S3 service: every path segment of the
    /// already percent-encoded wire path is URI-encoded once more, so a wire
    /// segment such as <c>anthropic.claude-3-sonnet-20240229-v1%3A0</c> becomes
    /// <c>anthropic.claude-3-sonnet-20240229-v1%253A0</c> in the canonical
    /// request.
    /// </summary>
    /// <remarks>
    /// AWS SigV4 verifies non-S3 requests against a double-encoded path: the
    /// request path is assumed to be encoded once for transmission and the
    /// canonical form encodes it again (the AWS SDKs' default
    /// <c>doubleEncode</c>/<c>use_double_uri_encode</c> behavior; only Amazon
    /// S3 canonicalizes the wire path as-is). Decoding the segment first and
    /// encoding it once produces a signature that services such as Bedrock
    /// Runtime reject for any model ID containing a colon. Dot-segment
    /// normalization is already applied by <see cref="Uri.AbsolutePath"/>.
    /// </remarks>
    /// <param name="uri">The absolute request URI whose wire path is canonicalized.</param>
    /// <returns>The canonical URI, or <c>/</c> when the path is empty.</returns>
    private static string BuildCanonicalUri(Uri uri)
    {
        Debug.Assert(uri.IsAbsoluteUri, "The caller validates the URI is absolute before canonicalization.");

        var absolutePath = uri.AbsolutePath;
        if (absolutePath.Length == 0)
        {
            return "/";
        }

        var segments = absolutePath.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            segments[i] = UriEncode(segments[i], encodeSlash: true);
        }

        return string.Join('/', segments);
    }

    private static string BuildCanonicalQueryString(Uri uri)
    {
        var query = uri.Query;
        if (string.IsNullOrEmpty(query) || query == "?")
        {
            return string.Empty;
        }

        var pairs = query[1..]
            .Split('&')
            .Select(part =>
            {
                var separatorIndex = part.IndexOf('=');
                return separatorIndex < 0
                    ? (Key: UriEncode(Uri.UnescapeDataString(part), encodeSlash: false), Value: string.Empty)
                    : (Key: UriEncode(Uri.UnescapeDataString(part[..separatorIndex]), encodeSlash: false),
                        Value: UriEncode(Uri.UnescapeDataString(part[(separatorIndex + 1)..]), encodeSlash: false));
            })
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal);

        return string.Join('&', pairs.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string UriEncode(string value, bool encodeSlash)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char) b;
            _ = char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~' || (c == '/' && !encodeSlash)
                ? builder.Append(c)
                : builder.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string Trim(string value)
    {
        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);
        var previousWasSpace = false;
        foreach (var c in trimmed)
        {
            if (c == ' ')
            {
                if (!previousWasSpace)
                {
                    _ = builder.Append(c);
                }

                previousWasSpace = true;
            }
            else
            {
                _ = builder.Append(c);
                previousWasSpace = false;
            }
        }

        return builder.ToString();
    }

    private static byte[] DeriveSigningKey(string secretAccessKey, string dateStamp, string region, string service)
    {
        var dateKey = HmacSha256(Encoding.UTF8.GetBytes($"AWS4{secretAccessKey}"), dateStamp);
        var dateRegionKey = HmacSha256(dateKey, region);
        var dateRegionServiceKey = HmacSha256(dateRegionKey, service);
        return HmacSha256(dateRegionServiceKey, _terminator);
    }

    private static byte[] HmacSha256(byte[] key, string data) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}

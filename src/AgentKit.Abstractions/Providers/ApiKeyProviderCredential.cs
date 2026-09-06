// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A provider credential consisting of one static, long-lived API key
/// supplied by the application's configuration.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="ApiKey"/>. Equality and hashing over a secret value is
/// intentional here (values are compared, not logged), but callers must
/// still take care never to place an instance where it could be
/// serialized, logged, or otherwise made observable outside the request
/// pipeline that consumes it.
/// </remarks>
public sealed record ApiKeyProviderCredential: ProviderCredential
{
    /// <summary>Initializes a new instance of the <see cref="ApiKeyProviderCredential"/> record.</summary>
    /// <param name="apiKey">The non-empty API key text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="apiKey"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ApiKeyProviderCredential(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ApiKey = apiKey;
    }

    /// <summary>Gets the API key text.</summary>
    public string ApiKey { get; init; }
}

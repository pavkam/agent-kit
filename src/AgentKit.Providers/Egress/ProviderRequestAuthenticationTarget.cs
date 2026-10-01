// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

using System.Net.Http;
using System.Net.Http.Headers;

/// <summary>Adapts one prepared <see cref="HttpRequestMessage"/> and its frozen body to <see cref="IProviderAuthenticationTarget"/>.</summary>
/// <remarks>
/// The target is created after the body is frozen, so a signing lease sees exactly the bytes that are sent. Headers set by
/// a lease replace any header of the same name already on the in-memory message. The type holds no secret itself; secret
/// values pass straight through <see cref="SetHeader"/> to the message.
/// </remarks>
internal sealed class ProviderRequestAuthenticationTarget: IProviderAuthenticationTarget
{
    private const string _authorizationHeaderName = "Authorization";

    private readonly HttpRequestMessage _message;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a target over an in-memory request message.</summary>
    /// <param name="message">The prepared message whose headers receive authentication.</param>
    /// <param name="providerId">The provider the request is sent to.</param>
    /// <param name="scheme">The branded provider's API-key header shape.</param>
    /// <param name="content">The frozen body, or null when the request has none.</param>
    /// <param name="timeProvider">The injected clock that supplies <see cref="UtcNow"/>.</param>
    internal ProviderRequestAuthenticationTarget(
        HttpRequestMessage message,
        ProviderId providerId,
        ProviderAuthorizationScheme scheme,
        NetworkRequestContent? content,
        TimeProvider timeProvider)
    {
        Debug.Assert(message is not null, "The caller supplies the prepared message.");
        Debug.Assert(message.RequestUri is { IsAbsoluteUri: true }, "The request validated an absolute address.");
        Debug.Assert(scheme is not null, "The caller supplies the branded scheme.");
        Debug.Assert(timeProvider is not null, "The caller supplies the injected clock.");

        _message = message;
        _timeProvider = timeProvider;
        ProviderId = providerId;
        Scheme = scheme;
        ContentType = content?.ContentType;
        Body = content?.Body ?? ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc/>
    public ProviderId ProviderId { get; }

    /// <inheritdoc/>
    public ProviderAuthorizationScheme Scheme { get; }

    /// <inheritdoc/>
    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    /// <inheritdoc/>
    public string Method => _message.Method.Method.ToUpperInvariant();

    /// <inheritdoc/>
    public Uri RequestUri => _message.RequestUri!;

    /// <inheritdoc/>
    public string? ContentType { get; }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Body { get; }

    /// <inheritdoc/>
    public void SetHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        _ = _message.Headers.Remove(name);
        if (string.Equals(name, _authorizationHeaderName, StringComparison.OrdinalIgnoreCase)
            && AuthenticationHeaderValue.TryParse(value, out var authorization))
        {
            _message.Headers.Authorization = authorization;
            return;
        }

        _ = _message.Headers.TryAddWithoutValidation(name, value);
    }
}

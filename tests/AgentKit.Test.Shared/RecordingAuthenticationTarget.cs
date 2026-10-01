// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="IProviderAuthenticationTarget"/> test double that describes one fixed request and records every
/// header a credential lease sets.
/// </summary>
public sealed class RecordingAuthenticationTarget: IProviderAuthenticationTarget
{
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a recording target.</summary>
    /// <param name="scheme">The branded API-key header shape, or null for the bearer default.</param>
    /// <param name="utcNow">The instant the target reports, or null for a fixed 2025-06-01 instant.</param>
    /// <param name="body">The frozen body bytes, or null for an empty body.</param>
    public RecordingAuthenticationTarget(
        ProviderAuthorizationScheme? scheme = null,
        DateTimeOffset? utcNow = null,
        ReadOnlyMemory<byte>? body = null)
    {
        Scheme = scheme ?? ProviderAuthorizationScheme.BearerToken;
        UtcNow = utcNow ?? new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
        Body = body ?? ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc/>
    public ProviderId ProviderId { get; } = new("test-provider");

    /// <inheritdoc/>
    public ProviderAuthorizationScheme Scheme { get; }

    /// <inheritdoc/>
    public DateTimeOffset UtcNow { get; }

    /// <inheritdoc/>
    public string Method => "POST";

    /// <inheritdoc/>
    public Uri RequestUri { get; } = new("https://api.example.test/v1/chat");

    /// <inheritdoc/>
    public string? ContentType => "application/json";

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> Body { get; }

    /// <summary>Gets the headers lease application has set, keyed case-insensitively.</summary>
    public IReadOnlyDictionary<string, string> Headers => _headers;

    /// <inheritdoc/>
    public void SetHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _headers[name] = value;
    }
}

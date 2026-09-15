// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

using System.Text.Json;

/// <summary>
/// Retains the human-readable message a provider placed in an HTTP error
/// body as bounded diagnostic evidence inside
/// <see cref="ProviderFailure.Extensions"/>, keeping it out of
/// <see cref="ProviderFailure.SafeMessage"/>.
/// </summary>
/// <remarks>
/// <para>
/// A provider's error text is untrusted content: it can echo the credential
/// that failed, name internal tenants or resources, or carry
/// prompt-injection payloads, and vendors change its wording without
/// notice. <see cref="ProviderFailure.SafeMessage"/> is documented as safe
/// for end users and model-visible tool errors, so every first-party adapter
/// reports a fixed status template there and keeps the machine-readable
/// code in <see cref="ProviderFailure.ProviderCode"/>. The provider's prose
/// is still valuable when an operator investigates a failure, so this type
/// stores it under one stable key that logging and diagnostics can read
/// deliberately. Like <see cref="ProviderFailure.DiagnosticCause"/>, the
/// retained text is classified diagnostic content and must not be surfaced
/// to users or models by default.
/// </para>
/// <para>
/// The retained message is bounded to <see cref="MaxLength"/> UTF-16 code
/// units so an oversized or adversarial error body cannot inflate a failure
/// record; longer text is truncated and the truncation is not marked. A
/// missing, empty, or whitespace-only message produces
/// <see cref="ExtensionData.Empty"/> so callers never observe an empty
/// evidence entry.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class ProviderErrorMessageEvidence
{
    /// <summary>
    /// The <see cref="ExtensionData"/> key under which the provider's error
    /// message is retained. The value is a JSON string.
    /// </summary>
    public const string Key = "agentkit.provider.error_message";

    /// <summary>
    /// The maximum number of UTF-16 code units retained from a provider
    /// error message. Longer messages are truncated to this length.
    /// </summary>
    public const int MaxLength = 2048;

    /// <summary>
    /// Builds the extension data that retains <paramref name="providerMessage"/>
    /// as diagnostic evidence, or <see cref="ExtensionData.Empty"/> when the
    /// provider supplied no usable message.
    /// </summary>
    /// <param name="providerMessage">
    /// The provider's human-readable error message as read from the error
    /// body, or <see langword="null"/> when the body carried none. Leading
    /// and trailing whitespace is removed and the result is bounded to
    /// <see cref="MaxLength"/> code units.
    /// </param>
    /// <returns>
    /// Extension data holding exactly one entry under <see cref="Key"/>, or
    /// <see cref="ExtensionData.Empty"/> when <paramref name="providerMessage"/>
    /// is <see langword="null"/>, empty, or whitespace.
    /// </returns>
    public static ExtensionData Create(string? providerMessage)
    {
        if (string.IsNullOrWhiteSpace(providerMessage))
        {
            return ExtensionData.Empty;
        }

        var trimmed = providerMessage.Trim();
        var bounded = trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed;

        return new ExtensionData(
            ImmutableDictionary<string, ExtensionValue>.Empty.Add(
                Key,
                new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(bounded)])));
    }

    /// <summary>
    /// Reads the retained provider error message from
    /// <paramref name="extensions"/>, when one is present.
    /// </summary>
    /// <param name="extensions">The failure's extension data to inspect.</param>
    /// <returns>
    /// The retained message; or <see langword="null"/> when
    /// <paramref name="extensions"/> has no entry under <see cref="Key"/> or
    /// the entry is not a JSON string.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is <see langword="null"/>.</exception>
    public static string? TryRead(ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        if (!extensions.Values.TryGetValue(Key, out var value))
        {
            return null;
        }

        var reader = new Utf8JsonReader(value.CanonicalJson.AsSpan());
        return reader.Read() && reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// JSON helpers shared by first-party provider request translators and
/// response parsers so that extension passthrough and tool-argument parsing
/// follow one rule in every package.
/// </summary>
/// <remarks>
/// <para>
/// These helpers are deliberately provider-neutral: they know nothing about
/// any wire schema and never decide which fields a translator owns. A
/// translator builds its own body first and then calls
/// <see cref="ApplyExtensions"/>, which is what makes the "extensions never
/// override an owned field" rule hold.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class ProviderJson
{
    private const string _emptyObjectJson = "{}";

    /// <summary>
    /// Copies every entry of <paramref name="extensions"/> into
    /// <paramref name="body"/> as a parsed JSON node, skipping any key the
    /// body already defines.
    /// </summary>
    /// <param name="body">The request body under construction. Mutated in place.</param>
    /// <param name="extensions">
    /// Provider-specific passthrough values keyed by the exact wire property
    /// name. Each value's canonical JSON is parsed into a node and attached
    /// under its key.
    /// </param>
    /// <remarks>
    /// Extension data never overrides a field the translator itself owns: a
    /// key that is already present in <paramref name="body"/> is left
    /// untouched, so a protected core field cannot be reshaped by a
    /// passthrough option. Entries are applied in the enumeration order of
    /// <see cref="ExtensionData.Values"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> or <paramref name="extensions"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">An extension value's canonical JSON could not be parsed into a node.</exception>
    public static void ApplyExtensions(JsonObject body, ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(extensions);

        foreach (var (key, value) in extensions.Values)
        {
            if (body.ContainsKey(key))
            {
                continue;
            }

            body[key] = JsonNode.Parse(value.CanonicalJson.AsSpan());
        }
    }

    /// <summary>
    /// Produces a detached <see cref="JsonElement"/> representing an empty
    /// JSON object, for tool calls whose provider omitted an arguments value.
    /// </summary>
    /// <returns>
    /// An element of kind <see cref="JsonValueKind.Object"/> with no
    /// properties, cloned out of its backing document so the caller may
    /// retain it indefinitely.
    /// </returns>
    public static JsonElement ParseEmptyObject()
    {
        using var document = JsonDocument.Parse(_emptyObjectJson);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Parses raw tool-call argument text into a detached
    /// <see cref="JsonElement"/>, treating an absent value as an empty object.
    /// </summary>
    /// <param name="json">The raw argument text as the provider sent it, or <see langword="null"/>.</param>
    /// <returns>
    /// <see cref="ParseEmptyObject"/> when <paramref name="json"/> is
    /// <see langword="null"/> or empty; otherwise the parsed root element
    /// cloned out of its backing document.
    /// </returns>
    /// <remarks>
    /// Only a missing value defaults to <c>{}</c>. Whitespace-only or
    /// malformed text is not silently replaced, because that would fabricate
    /// a well-formed call the model never made; it propagates as a
    /// <see cref="JsonException"/> for the caller to report as a protocol
    /// failure.
    /// </remarks>
    /// <exception cref="JsonException"><paramref name="json"/> is non-empty and is not valid JSON.</exception>
    public static JsonElement ParseArguments(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return ParseEmptyObject();
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Writes every string as one fixed constant so a configured contract provably cannot reproduce persisted evidence.</summary>
/// <remarks>
/// <see cref="JsonStoreSerialization.VerifyRoundTrip{TValue}"/> exists precisely because callers may replace the encoding
/// contract with options that encode successfully yet decode to a different value. A converter that silently rewrites text is
/// the smallest faithful model of that failure: encoding and decoding both succeed, so only the equality check can detect the
/// loss.
/// </remarks>
internal sealed class ConstantTextJsonConverter: JsonConverter<string>
{
    /// <summary>Gets the constant text this converter substitutes for every written string.</summary>
    /// <value>A value deliberately different from any sample identifier, so substitution is always observable.</value>
    internal static string Constant => "substituted";

    /// <summary>Reads a string verbatim, so decoding never masks what writing substituted.</summary>
    /// <param name="reader">The reader positioned on the string token.</param>
    /// <param name="typeToConvert">The requested type, always <see cref="string"/>.</param>
    /// <param name="options">The effective contract, unused because reading is verbatim.</param>
    /// <returns>The exact persisted text, or null for a JSON null token.</returns>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString();

    /// <summary>Writes <see cref="Constant"/> regardless of the supplied value.</summary>
    /// <param name="writer">The writer receiving the substituted text.</param>
    /// <param name="value">The original value, deliberately discarded.</param>
    /// <param name="options">The effective contract, unused because the substitution is unconditional.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is null.</exception>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(Constant);
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The application-facing terminal value produced by one accepted output
/// candidate.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Exactly
/// one of <see cref="Text"/> or <see cref="Json"/> is populated, matching
/// the producing definition's <see cref="OutputDefinition.Mode"/>:
/// <see cref="OutputMode.Text"/> populates <see cref="Text"/>; every
/// structured mode populates <see cref="Json"/>.
/// </para>
/// <para>
/// A declarative JSON schema without a declared
/// <see cref="OutputDefinition.RuntimeType"/> yields a validated
/// <see cref="Json"/> value with <see cref="Value"/> left
/// <see langword="null"/>: schema validation alone is not a promise of a
/// safe application object. <see cref="Value"/> is populated only when a
/// runtime type was declared and deserialization succeeded.
/// </para>
/// </remarks>
public sealed record ValidatedOutput
{
    /// <summary>Initializes a new instance of the <see cref="ValidatedOutput"/> record.</summary>
    /// <param name="mode">The output mode this value was produced under.</param>
    /// <param name="text">The validated text, when <paramref name="mode"/> is <see cref="OutputMode.Text"/>.</param>
    /// <param name="json">The validated structured value, when <paramref name="mode"/> is a structured mode.</param>
    /// <param name="value">The deserialized application value, when a runtime type was declared and deserialization succeeded.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is undefined.</exception>
    public ValidatedOutput(OutputMode mode, string? text, JsonElement? json, object? value)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(mode);

        Mode = mode;
        Text = text;
        Json = json;
        Value = value;
    }

    /// <summary>Gets the output mode this value was produced under.</summary>
    public OutputMode Mode { get; init; }

    /// <summary>Gets the validated text, when <see cref="Mode"/> is <see cref="OutputMode.Text"/>.</summary>
    public string? Text { get; init; }

    /// <summary>Gets the validated structured value, when <see cref="Mode"/> is a structured mode.</summary>
    public JsonElement? Json { get; init; }

    /// <summary>
    /// Gets the deserialized application value, when a runtime type was
    /// declared and deserialization succeeded.
    /// </summary>
    public object? Value { get; init; }

    /// <inheritdoc/>
    public bool Equals(ValidatedOutput? other) =>
        other is not null
        && Mode == other.Mode
        && Text == other.Text
        && JsonElementEquals(Json, other.Json)
        && Equals(Value, other.Value);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Mode);
        hash.Add(Text);
        hash.Add(Json?.GetRawText());
        hash.Add(Value);
        return hash.ToHashCode();
    }

    private static bool JsonElementEquals(JsonElement? left, JsonElement? right) =>
        left is null || right is null
            ? left is null && right is null
            : left.Value.GetRawText() == right.Value.GetRawText();
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable, catalog-facing description of one tool: its identity,
/// display metadata, declared input shape, and coarse effect category.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately reduced stand-in for the fuller tool descriptor
/// described by the tools architecture, which additionally carries
/// provider-facing schema translation hints, deprecation and replacement
/// metadata, and idempotency declarations. Until a full catalog/versioning
/// system exists, one <see cref="ToolId"/> maps to exactly one descriptor
/// per <see cref="IToolCatalog"/>; version-aware resolution is deferred.
/// </para>
/// </remarks>
public sealed record ToolDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="ToolDescriptor"/> record.</summary>
    /// <param name="id">The stable identity of this tool.</param>
    /// <param name="version">The published version of this tool, when versioned.</param>
    /// <param name="name">The display name advertised to the model.</param>
    /// <param name="description">A human- and model-readable description of what the tool does.</param>
    /// <param name="inputSchema">The JSON Schema describing valid call arguments.</param>
    /// <param name="effect">The coarse effect category this tool declares.</param>
    /// <param name="extensions">Provider-specific or forward-compatible descriptor data.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or <paramref name="description"/> is null,
    /// empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ToolDescriptor(
        ToolId id,
        ToolVersion? version,
        string name,
        string description,
        JsonElement inputSchema,
        ToolEffect effect,
        ExtensionData extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(extensions);

        Id = id;
        Version = version;
        Name = name;
        Description = description;
        InputSchema = inputSchema;
        Effect = effect;
        Extensions = extensions;
    }

    /// <summary>Gets the stable identity of this tool.</summary>
    public ToolId Id { get; init; }

    /// <summary>Gets the published version of this tool, when versioned.</summary>
    public ToolVersion? Version { get; init; }

    /// <summary>Gets the display name advertised to the model.</summary>
    public string Name { get; init; }

    /// <summary>Gets a human- and model-readable description of what the tool does.</summary>
    public string Description { get; init; }

    /// <summary>Gets the JSON Schema describing valid call arguments.</summary>
    public JsonElement InputSchema { get; init; }

    /// <summary>Gets the coarse effect category this tool declares.</summary>
    public ToolEffect Effect { get; init; }

    /// <summary>Gets provider-specific or forward-compatible descriptor data.</summary>
    public ExtensionData Extensions { get; init; }

    /// <inheritdoc/>
    public bool Equals(ToolDescriptor? other) =>
        other is not null
        && Id.Equals(other.Id)
        && Nullable.Equals(Version, other.Version)
        && Name == other.Name
        && Description == other.Description
        && RawTextOf(InputSchema) == RawTextOf(other.InputSchema)
        && Effect == other.Effect
        && Extensions.Equals(other.Extensions);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Id, Version, Name, Description, RawTextOf(InputSchema), Effect, Extensions);

    private static string? RawTextOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.Undefined ? null : element.GetRawText();
}

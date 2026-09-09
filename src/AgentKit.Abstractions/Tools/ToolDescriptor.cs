// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one exact, source-owned tool contract advertised through a catalog snapshot.</summary>
/// <remarks>
/// The immutable descriptor owns its schemas and carries declarations only. Descriptions, schemas, effects, and hints
/// are untrusted inputs to validation and policy; none grants authority or proves runtime support.
/// </remarks>
public sealed record ToolDescriptor
{
    /// <summary>Initializes a complete immutable tool descriptor.</summary>
    /// <param name="id">The nondefault canonical tool identity.</param>
    /// <param name="version">The nondefault published contract version.</param>
    /// <param name="name">The nonblank display name advertised to the model.</param>
    /// <param name="description">The nonblank human- and model-readable description.</param>
    /// <param name="inputSchema">The nonnull owned canonical input schema.</param>
    /// <param name="outputSchema">The optional owned canonical output schema.</param>
    /// <param name="effects">The nonnull effect declarations.</param>
    /// <param name="executionHints">The nonnull optional execution hints.</param>
    /// <param name="sourceId">The nondefault explicit publishing source.</param>
    /// <param name="extensions">The nonnull immutable extension evidence.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/>, <paramref name="version"/>, or <paramref name="sourceId"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="description"/>, <paramref name="inputSchema"/>, <paramref name="effects"/>, <paramref name="executionHints"/>, or <paramref name="extensions"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="description"/> is empty or whitespace.</exception>
    public ToolDescriptor(
        ToolId id,
        ToolVersion version,
        string name,
        string description,
        JsonSchema inputSchema,
        JsonSchema? outputSchema,
        ToolEffects effects,
        ToolExecutionHints executionHints,
        ToolSourceId sourceId,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(inputSchema);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(executionHints);
        ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
        ArgumentNullException.ThrowIfNull(extensions);
        Id = id;
        Version = version;
        Name = name;
        Description = description;
        InputSchema = inputSchema;
        OutputSchema = outputSchema;
        Effects = effects;
        ExecutionHints = executionHints;
        SourceId = sourceId;
        Extensions = extensions;
    }

    /// <summary>Gets the canonical tool identity.</summary>
    /// <value>A nondefault catalog identity distinct from any provider-visible alias.</value>
    public ToolId Id { get; }
    /// <summary>Gets the exact published contract version.</summary>
    /// <value>A nondefault version captured with the descriptor and used in exact catalog resolution.</value>
    public ToolVersion Version { get; }
    /// <summary>Gets the advertised display name.</summary>
    /// <value>Nonblank descriptive text; provider aliases are captured separately by a catalog snapshot.</value>
    public string Name { get; }
    /// <summary>Gets the human- and model-readable description.</summary>
    /// <value>Nonblank untrusted metadata that grants no authority or capability.</value>
    public string Description { get; }
    /// <summary>Gets the owned canonical input schema.</summary>
    /// <value>An immutable dialect-bound schema value; support still requires selected-engine preflight.</value>
    public JsonSchema InputSchema { get; }
    /// <summary>Gets the optional owned canonical output schema.</summary>
    /// <value>An immutable dialect-bound schema, or null when no output schema is declared.</value>
    public JsonSchema? OutputSchema { get; }
    /// <summary>Gets the declared effect evidence.</summary>
    /// <value>Immutable untrusted policy input that never grants permission or proves replay safety.</value>
    public ToolEffects Effects { get; }
    /// <summary>Gets the optional execution hints.</summary>
    /// <value>Immutable advisory evidence that host policy may tighten and must not treat as authority.</value>
    public ToolExecutionHints ExecutionHints { get; }
    /// <summary>Gets the explicit publishing source.</summary>
    /// <value>A nondefault stable package or registration source identity, never inferred from CLR activation.</value>
    public ToolSourceId SourceId { get; }
    /// <summary>Gets immutable extension evidence.</summary>
    /// <value>Provider-specific or forward-compatible values that do not replace typed descriptor fields.</value>
    public ExtensionData Extensions { get; }
}

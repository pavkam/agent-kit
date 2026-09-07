// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable, declarative description of one agent an engine can host and
/// run.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state, holds
/// no service instances, and is safe to share across threads without
/// synchronization. One engine may host many definitions and run them
/// concurrently.
/// </para>
/// <para>
/// A definition is the reusable half of a run: it states which models the
/// agent may use, what its instructions and tools are, and what its default
/// limits are. The other half — session, branch, execution identity, and run
/// identity — is supplied per invocation, which is why none of those appear
/// here.
/// </para>
/// <para>
/// A definition selects behavior; it never contains it. There is no service,
/// provider client, credential, or open resource on this record, so it can be
/// cataloged, compared by value, versioned, and safely retained.
/// </para>
/// </remarks>
public sealed record AgentDefinition
{
    private readonly string _displayName;
    private readonly ModelSelectionPolicy _models;
    private readonly ModelRequirements _modelRequirements;
    private readonly ImmutableArray<AgentMessage> _instructions;
    private readonly ImmutableArray<LlmToolDefinition> _tools;
    private readonly LlmToolChoice _toolChoice;
    private readonly LlmRequestSettings _settings;
    private readonly RunPolicyDefaults _runDefaults;
    private readonly ExtensionData _extensions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentDefinition"/> record.
    /// </summary>
    /// <param name="id">The agent's stable identity.</param>
    /// <param name="revision">This definition's content revision.</param>
    /// <param name="displayName">
    /// A human-readable name used in diagnostics and operator tooling.
    /// </param>
    /// <param name="models">
    /// The candidate and fallback policy used to choose this agent's model.
    /// </param>
    /// <param name="modelRequirements">
    /// The portable behaviors this agent's requests need.
    /// </param>
    /// <param name="instructions">
    /// The system and developer instructions placed first in every request.
    /// </param>
    /// <param name="tools">The tools this agent may call.</param>
    /// <param name="toolChoice">The tool-call selection policy.</param>
    /// <param name="settings">The effective sampling and output settings.</param>
    /// <param name="runDefaults">
    /// The default run limits applied when a caller does not override them.
    /// </param>
    /// <param name="extensions">
    /// Application-specific or forward-compatible definition data.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="id"/> is the default, empty identity.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="displayName"/> is null, empty, or whitespace-only, or
    /// <paramref name="instructions"/> or <paramref name="tools"/> is
    /// uninitialized or contains <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="models"/>, <paramref name="modelRequirements"/>,
    /// <paramref name="toolChoice"/>, <paramref name="settings"/>,
    /// <paramref name="runDefaults"/>, or <paramref name="extensions"/> is
    /// <see langword="null"/>.
    /// </exception>
    public AgentDefinition(
        AgentId id,
        AgentDefinitionRevision revision,
        string displayName,
        ModelSelectionPolicy models,
        ModelRequirements modelRequirements,
        ImmutableArray<AgentMessage> instructions,
        ImmutableArray<LlmToolDefinition> tools,
        LlmToolChoice toolChoice,
        LlmRequestSettings settings,
        RunPolicyDefaults runDefaults,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(modelRequirements);
        ArgumentException.ThrowIfContainsNull(instructions);
        ArgumentException.ThrowIfContainsNull(tools);
        ArgumentNullException.ThrowIfNull(toolChoice);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(runDefaults);
        ArgumentNullException.ThrowIfNull(extensions);

        Id = id;
        Revision = revision;
        _displayName = displayName;
        _models = models;
        _modelRequirements = modelRequirements;
        _instructions = instructions;
        _tools = tools;
        _toolChoice = toolChoice;
        _settings = settings;
        _runDefaults = runDefaults;
        _extensions = extensions;
    }

    /// <summary>Gets the agent's stable identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set the default, empty identity.
    /// </exception>
    public AgentId Id
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(Id));
            field = value;
        }
    }

    /// <summary>Gets this definition's content revision.</summary>
    public AgentDefinitionRevision Revision { get; init; }

    /// <summary>Gets the human-readable name used in diagnostics.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string DisplayName
    {
        get => _displayName;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(DisplayName));
            _displayName = value;
        }
    }

    /// <summary>Gets the candidate and fallback policy for model choice.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ModelSelectionPolicy Models
    {
        get => _models;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Models));
            _models = value;
        }
    }

    /// <summary>Gets the portable behaviors this agent's requests need.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ModelRequirements ModelRequirements
    {
        get => _modelRequirements;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ModelRequirements));
            _modelRequirements = value;
        }
    }

    /// <summary>Gets the instructions placed first in every request.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized array or one
    /// containing <see langword="null"/>.
    /// </exception>
    public ImmutableArray<AgentMessage> Instructions
    {
        get => _instructions;
        init
        {
            ArgumentException.ThrowIfContainsNull(value, nameof(Instructions));
            _instructions = value;
        }
    }

    /// <summary>Gets the tools this agent may call.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized array or one
    /// containing <see langword="null"/>.
    /// </exception>
    public ImmutableArray<LlmToolDefinition> Tools
    {
        get => _tools;
        init
        {
            ArgumentException.ThrowIfContainsNull(value, nameof(Tools));
            _tools = value;
        }
    }

    /// <summary>Gets the tool-call selection policy.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public LlmToolChoice ToolChoice
    {
        get => _toolChoice;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ToolChoice));
            _toolChoice = value;
        }
    }

    /// <summary>Gets the effective sampling and output settings.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public LlmRequestSettings Settings
    {
        get => _settings;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Settings));
            _settings = value;
        }
    }

    /// <summary>Gets the default run limits.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public RunPolicyDefaults RunDefaults
    {
        get => _runDefaults;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(RunDefaults));
            _runDefaults = value;
        }
    }

    /// <summary>Gets application-specific definition data.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ExtensionData Extensions
    {
        get => _extensions;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Extensions));
            _extensions = value;
        }
    }

    /// <summary>
    /// Determines whether this definition has the same identity, revision,
    /// and declarative content as <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The definition to compare, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when every field is equal and the ordered
    /// instruction and tool collections have equal contents.
    /// </returns>
    /// <remarks>
    /// Catalog reloads may reconstruct immutable arrays, so reference equality
    /// for those arrays is insufficient to establish that a pinned definition
    /// remains available for admission.
    /// </remarks>
    public bool Equals(AgentDefinition? other) =>
        other is not null
        && Id.Equals(other.Id)
        && Revision.Equals(other.Revision)
        && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal)
        && Models.Equals(other.Models)
        && ModelRequirements.Equals(other.ModelRequirements)
        && Instructions.SequenceEqual(other.Instructions)
        && Tools.SequenceEqual(other.Tools)
        && ToolChoice.Equals(other.ToolChoice)
        && Settings.Equals(other.Settings)
        && RunDefaults.Equals(other.RunDefaults)
        && Extensions.Equals(other.Extensions);

    /// <summary>
    /// Returns a hash code consistent with structural definition equality.
    /// </summary>
    /// <returns>
    /// A hash code over every scalar field and every ordered instruction and
    /// tool entry.
    /// </returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Revision);
        hash.Add(DisplayName, StringComparer.Ordinal);
        hash.Add(Models);
        hash.Add(ModelRequirements);
        foreach (var instruction in Instructions)
        {
            hash.Add(instruction);
        }

        foreach (var tool in Tools)
        {
            hash.Add(tool);
        }

        hash.Add(ToolChoice);
        hash.Add(Settings);
        hash.Add(RunDefaults);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}

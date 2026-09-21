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
    private readonly ImmutableArray<InstructionSource> _instructionSources;
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
    /// <remarks>
    /// This compatibility constructor creates an explicitly unconfigured definition. The
    /// resulting value can still be cataloged and inspected, but composition validation
    /// rejects it as unrunnable until a revised definition selects both profiles through
    /// the profile-aware constructor.
    /// </remarks>
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
        _instructionSources = InstructionSourceProjection.FromLegacyMessages(instructions, revision);
        _tools = tools;
        _toolChoice = toolChoice;
        _settings = settings;
        _runDefaults = runDefaults;
        _extensions = extensions;
    }

    /// <summary>Initializes a runnable definition with explicit security and session profile selections.</summary>
    /// <param name="id">The agent's stable identity.</param>
    /// <param name="revision">This definition's content revision.</param>
    /// <param name="displayName">The human-readable diagnostic name.</param>
    /// <param name="models">The candidate and fallback model policy.</param>
    /// <param name="modelRequirements">The portable model behaviors required.</param>
    /// <param name="instructions">The ordered system and developer instructions.</param>
    /// <param name="tools">The tools this agent may call.</param>
    /// <param name="toolChoice">The tool-call selection policy.</param>
    /// <param name="settings">The effective model request settings.</param>
    /// <param name="runDefaults">The default bounded run limits.</param>
    /// <param name="extensions">Application-specific immutable definition data.</param>
    /// <param name="securityProfile">The explicitly selected nonblank security profile key.</param>
    /// <param name="sessionProfile">The explicitly selected nonblank session profile key.</param>
    /// <exception cref="ArgumentException">A profile key is blank, or an inherited definition constraint is invalid.</exception>
    /// <exception cref="ArgumentNullException">An inherited required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
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
        ExtensionData extensions,
        SecurityProfileKey securityProfile,
        SessionProfileKey sessionProfile)
        : this(id, revision, displayName, models, modelRequirements, instructions, tools, toolChoice, settings,
            runDefaults, extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityProfile.Value, nameof(securityProfile));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionProfile.Value, nameof(sessionProfile));
        SecurityProfile = securityProfile;
        SessionProfile = sessionProfile;
    }

    /// <summary>Initializes a runnable definition from explicit instruction sources.</summary>
    /// <param name="id">The agent's stable identity.</param>
    /// <param name="revision">This definition's content revision.</param>
    /// <param name="displayName">The human-readable diagnostic name.</param>
    /// <param name="models">The candidate and fallback model policy.</param>
    /// <param name="modelRequirements">The portable model behaviors required.</param>
    /// <param name="instructionSources">The ordered instruction sources placed first in every request.</param>
    /// <param name="tools">The tools this agent may call.</param>
    /// <param name="toolChoice">The tool-call selection policy.</param>
    /// <param name="settings">The effective model request settings.</param>
    /// <param name="runDefaults">The default bounded run limits.</param>
    /// <param name="extensions">Application-specific immutable definition data.</param>
    /// <param name="securityProfile">The explicitly selected nonblank security profile key.</param>
    /// <param name="sessionProfile">The explicitly selected nonblank session profile key.</param>
    /// <exception cref="ArgumentException">A profile key is blank, an array contains null, or a source is unsupported.</exception>
    /// <exception cref="ArgumentNullException">An inherited required reference is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    /// <remarks>
    /// Flat <see cref="Instructions"/> remain available as a projection of literal sources until a later
    /// workstream makes instruction sources the sole authoring surface.
    /// </remarks>
    public AgentDefinition(
        AgentId id,
        AgentDefinitionRevision revision,
        string displayName,
        ModelSelectionPolicy models,
        ModelRequirements modelRequirements,
        AgentInstructionSources instructionSources,
        ImmutableArray<LlmToolDefinition> tools,
        LlmToolChoice toolChoice,
        LlmRequestSettings settings,
        RunPolicyDefaults runDefaults,
        ExtensionData extensions,
        SecurityProfileKey securityProfile,
        SessionProfileKey sessionProfile)
        : this(
            id,
            revision,
            displayName,
            models,
            modelRequirements,
            InstructionSourceProjection.ToMessages(instructionSources.Sources),
            tools,
            toolChoice,
            settings,
            runDefaults,
            extensions,
            securityProfile,
            sessionProfile)
        => _instructionSources = instructionSources.Sources;

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

    /// <summary>Gets the explicitly selected security profile key.</summary>
    /// <value>A nonblank key for runnable definitions; default only on the retained legacy unrunnable shape.</value>
    public SecurityProfileKey SecurityProfile { get; }

    /// <summary>Gets the explicitly selected session profile key.</summary>
    /// <value>A nonblank key for runnable definitions; default only on the retained legacy unrunnable shape.</value>
    public SessionProfileKey SessionProfile { get; }

    /// <summary>Gets the hook profile whose captured catalog and registrations this agent's runs dispatch through.</summary>
    /// <value>
    /// Defaults to <see cref="HookRegistrationDescriptors.DefaultProfileKey"/> when unset. Composition validation
    /// resolves this key through <see cref="IHookProfileSelector"/> for every published definition.
    /// </value>
    public HookProfileKey HookProfile { get; init; } = HookRegistrationDescriptors.DefaultProfileKey;

    /// <summary>Gets the explicitly selected keyed <see cref="IAgentLoop"/> this definition runs under.</summary>
    /// <value>
    /// The selected key, or <see langword="null"/> when this definition leaves the loop unspecified. A
    /// <see langword="null"/> value resolves to <see cref="AgentLoopComponentDefaults.LoopKey"/> at run
    /// activation rather than to an ambient unkeyed registration, so every definition — configured or not —
    /// always names an exact keyed <see cref="IAgentLoop"/> selection. This lets one engine host several
    /// definitions that each select a different keyed loop, and therefore a different compiled
    /// <see cref="AgentRunServices"/> bundle, without any definition depending on Microsoft DI types.
    /// </value>
    public ComponentKey<IAgentLoop>? LoopKey { get; init; }

    /// <summary>Gets the explicitly selected keyed <see cref="IInputCoordinator"/> this definition promotes admitted input through.</summary>
    /// <value>
    /// The selected key, or <see langword="null"/> when this definition leaves the input coordinator unspecified.
    /// A <see langword="null"/> value resolves to <see cref="AgentIOComponentDefaults.InputCoordinatorKey"/> at
    /// run activation. Unlike <see cref="LoopKey"/> this collaborator is optional: a composition that registers
    /// nothing for the resolved key simply never promotes mid-run input.
    /// </value>
    public ComponentKey<IInputCoordinator>? InputCoordinatorKey { get; init; }

    /// <summary>Gets the explicitly selected keyed <see cref="IOutputPublisher"/> this definition publishes run events and its final result through.</summary>
    /// <value>
    /// The selected key, or <see langword="null"/> when this definition leaves the output publisher unspecified.
    /// A <see langword="null"/> value resolves to <see cref="AgentIOComponentDefaults.OutputPublisherKey"/> at
    /// run activation. Unlike <see cref="LoopKey"/> this collaborator is optional.
    /// </value>
    public ComponentKey<IOutputPublisher>? OutputPublisherKey { get; init; }

    /// <summary>Gets the structured-output contract every run of this definition must satisfy before it completes.</summary>
    /// <value>
    /// The immutable definition the loop hands to the selected <see cref="IOutputProcessor"/> after each terminal
    /// assistant response, or <see langword="null"/> when the agent's final answer is free text. When set, a run
    /// completes only with an <see cref="OutputAccepted"/> decision; a rejected candidate is repaired within the
    /// definition's retry policy or the run halts with <see cref="RunPolicyHalted"/>.
    /// </value>
    /// <remarks>
    /// The definition owner is responsible for telling the model what to produce: in <see cref="OutputMode.Prompted"/>
    /// the schema reaches the model only through <see cref="Instructions"/>, which carry definition authority.
    /// </remarks>
    public OutputDefinition? Output { get; init; }

    /// <summary>Gets the budget limits every run of this definition reserves against.</summary>
    /// <value>
    /// Hard or soft limits over first-party or host dimensions, or empty when runs are bounded only by turns and
    /// timeouts. When non-empty the composition must select an <see cref="IBudgetAuthority"/>; the loop creates one
    /// run scope with these limits, reserves before each turn, model request, and tool call, accounts reported usage,
    /// and settles the run as <see cref="RunLimitReached"/> or <see cref="RunFailed"/> when a reservation is refused, depending on whether the refusal carried full budget-limit evidence.
    /// </value>
    /// <exception cref="ArgumentException">An initializer assigns a default array or one containing a null element.</exception>
    public ImmutableArray<BudgetLimit> BudgetLimits
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value, nameof(BudgetLimits));
            ArgumentException.ThrowIfContainsNull(value, nameof(BudgetLimits));
            field = value;
        }
    } = [];

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

    /// <summary>Gets the instruction sources placed first in every request.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized array or one containing <see langword="null"/>.
    /// </exception>
    public ImmutableArray<InstructionSource> InstructionSources
    {
        get => _instructionSources;
        init
        {
            ArgumentException.ThrowIfContainsNull(value, nameof(InstructionSources));
            _instructionSources = value;
        }
    }

    /// <summary>Gets the instructions placed first in every request.</summary>
    /// <value>A projection of literal instruction sources for compatibility with the reduced loop.</value>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized array or one
    /// containing <see langword="null"/>.
    /// </exception>
    public ImmutableArray<AgentMessage> Instructions
    {
        get => InstructionSourceProjection.ToMessages(_instructionSources);
        init
        {
            ArgumentException.ThrowIfContainsNull(value, nameof(Instructions));
            _instructionSources = InstructionSourceProjection.FromLegacyMessages(value, Revision);
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
        && SecurityProfile.Equals(other.SecurityProfile)
        && SessionProfile.Equals(other.SessionProfile)
        && LoopKey.Equals(other.LoopKey)
        && InputCoordinatorKey.Equals(other.InputCoordinatorKey)
        && OutputPublisherKey.Equals(other.OutputPublisherKey)
        && Equals(Output, other.Output)
        && BudgetLimits.SequenceEqual(other.BudgetLimits)
        && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal)
        && Models.Equals(other.Models)
        && ModelRequirements.Equals(other.ModelRequirements)
        && InstructionSources.SequenceEqual(other.InstructionSources)
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
        hash.Add(SecurityProfile);
        hash.Add(SessionProfile);
        hash.Add(LoopKey);
        hash.Add(InputCoordinatorKey);
        hash.Add(OutputPublisherKey);
        hash.Add(Output);
        foreach (var limit in BudgetLimits)
        {
            hash.Add(limit);
        }

        hash.Add(DisplayName, StringComparer.Ordinal);
        hash.Add(Models);
        hash.Add(ModelRequirements);
        foreach (var source in InstructionSources)
        {
            hash.Add(source);
        }

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

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
/// A definition is the reusable half of a run: it states which components,
/// profiles, models, instructions, toolsets, output contract, and default limits
/// the agent uses. The other half — session, branch, execution identity, and run
/// identity — is supplied per invocation, which is why none of those appear
/// here.
/// </para>
/// <para>
/// A definition selects behavior; it never contains it. There is no service,
/// provider client, credential, or open resource on this record, so it can be
/// cataloged, compared by value, versioned, and safely retained. Every required
/// selection is explicit: <see cref="Components"/> names each required keyed
/// collaborator, and <see cref="OptionalCapabilities"/> names only the optional
/// ones the agent enables.
/// </para>
/// </remarks>
public sealed record AgentDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentDefinition"/> record from every normative selection.
    /// </summary>
    /// <param name="id">The agent's stable identity.</param>
    /// <param name="revision">This definition's content revision.</param>
    /// <param name="displayName">A human-readable name used in diagnostics and operator tooling.</param>
    /// <param name="components">The keyed required collaborators this agent runs with.</param>
    /// <param name="sessionProfile">The explicitly selected session profile key.</param>
    /// <param name="hookProfile">The explicitly selected hook profile key.</param>
    /// <param name="securityProfile">The explicitly selected security profile key.</param>
    /// <param name="optionalCapabilities">The optional capabilities this agent enables, or <see cref="AgentOptionalCapabilitySelection.None"/>.</param>
    /// <param name="models">
    /// The candidate, fallback, requirement, and request-setting policy used to choose and drive this agent's model.
    /// </param>
    /// <param name="instructions">The ordered instruction sources placed first in every request.</param>
    /// <param name="toolsets">The authored toolset selections resolved through the registration catalog.</param>
    /// <param name="runDefaults">The default run limits applied when a caller does not override them.</param>
    /// <param name="output">The output contract every run must satisfy; a text definition expresses free-form output.</param>
    /// <param name="extensions">Application-specific or forward-compatible definition data.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is the default, empty identity.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="displayName"/> is null, empty, or whitespace-only; a profile key is the default or blank
    /// value; or <paramref name="instructions"/> or <paramref name="toolsets"/> is uninitialized or contains
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="components"/>, <paramref name="optionalCapabilities"/>, <paramref name="models"/>,
    /// <paramref name="runDefaults"/>, <paramref name="output"/>, or <paramref name="extensions"/> is
    /// <see langword="null"/>.
    /// </exception>
    public AgentDefinition(
        AgentId id,
        AgentDefinitionRevision revision,
        string displayName,
        AgentComponentSelection components,
        SessionProfileKey sessionProfile,
        HookProfileKey hookProfile,
        SecurityProfileKey securityProfile,
        AgentOptionalCapabilitySelection optionalCapabilities,
        ModelSelectionPolicy models,
        ImmutableArray<InstructionSource> instructions,
        ImmutableArray<ToolsetReference> toolsets,
        RunPolicyDefaults runDefaults,
        OutputDefinition output,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionProfile.Value, nameof(sessionProfile));
        ArgumentException.ThrowIfNullOrWhiteSpace(hookProfile.Value, nameof(hookProfile));
        ArgumentException.ThrowIfNullOrWhiteSpace(securityProfile.Value, nameof(securityProfile));
        ArgumentNullException.ThrowIfNull(optionalCapabilities);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentException.ThrowIfDefault(instructions);
        ArgumentException.ThrowIfContainsNull(instructions);
        ArgumentException.ThrowIfDefault(toolsets);
        ArgumentException.ThrowIfContainsNull(toolsets);
        ArgumentNullException.ThrowIfNull(runDefaults);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(extensions);

        Id = id;
        Revision = revision;
        DisplayName = displayName;
        Components = components;
        SessionProfile = sessionProfile;
        HookProfile = hookProfile;
        SecurityProfile = securityProfile;
        OptionalCapabilities = optionalCapabilities;
        Models = models;
        Instructions = instructions;
        Toolsets = toolsets;
        RunDefaults = runDefaults;
        Output = output;
        Extensions = extensions;
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
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(DisplayName));
            field = value;
        }
    }

    /// <summary>Gets the keyed required collaborators this agent runs with.</summary>
    /// <value>One nondefault key per required collaborator: loop, continuation policy, input, output, output processor, context, model selector, model executor, and budget profile.</value>
    /// <exception cref="ArgumentNullException">An initializer attempts to set <see langword="null"/>.</exception>
    public AgentComponentSelection Components
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Components));
            field = value;
        }
    }

    /// <summary>Gets the explicitly selected session profile key.</summary>
    /// <value>A nonblank key.</value>
    /// <exception cref="ArgumentException">An initializer attempts to set a default or blank key.</exception>
    public SessionProfileKey SessionProfile
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(SessionProfile));
            field = value;
        }
    }

    /// <summary>Gets the hook profile whose captured catalog and registrations this agent's runs dispatch through.</summary>
    /// <value>A nonblank key. Composition validation resolves it through <see cref="IHookProfileSelector"/> for every published definition.</value>
    /// <exception cref="ArgumentException">An initializer attempts to set a default or blank key.</exception>
    public HookProfileKey HookProfile
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(HookProfile));
            field = value;
        }
    }

    /// <summary>Gets the explicitly selected security profile key.</summary>
    /// <value>A nonblank key.</value>
    /// <exception cref="ArgumentException">An initializer attempts to set a default or blank key.</exception>
    public SecurityProfileKey SecurityProfile
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(SecurityProfile));
            field = value;
        }
    }

    /// <summary>Gets the optional capabilities this agent enables.</summary>
    /// <value><see cref="AgentOptionalCapabilitySelection.None"/> when no optional capability is enabled.</value>
    /// <exception cref="ArgumentNullException">An initializer attempts to set <see langword="null"/>.</exception>
    public AgentOptionalCapabilitySelection OptionalCapabilities
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(OptionalCapabilities));
            field = value;
        }
    }

    /// <summary>Gets the candidate, fallback, requirement, and request-setting policy for model choice.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ModelSelectionPolicy Models
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Models));
            field = value;
        }
    }

    /// <summary>Gets the ordered instruction sources placed first in every request.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized array or one containing <see langword="null"/>.
    /// </exception>
    public ImmutableArray<InstructionSource> Instructions
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value, nameof(Instructions));
            ArgumentException.ThrowIfContainsNull(value, nameof(Instructions));
            field = value;
        }
    }

    /// <summary>Gets the authored toolset selections resolved through the registration catalog at run time.</summary>
    /// <value>
    /// An initialized sequence of toolset references; empty when the agent does not select toolsets for discovery.
    /// </value>
    /// <exception cref="ArgumentException">An initializer supplies an uninitialized array or a null entry.</exception>
    public ImmutableArray<ToolsetReference> Toolsets
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value, nameof(Toolsets));
            ArgumentException.ThrowIfContainsNull(value, nameof(Toolsets));
            field = value;
        }
    }

    /// <summary>Gets the default run limits.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public RunPolicyDefaults RunDefaults
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(RunDefaults));
            field = value;
        }
    }

    /// <summary>Gets the output contract every run of this definition must satisfy before it completes.</summary>
    /// <value>
    /// The immutable definition the loop hands to the selected <see cref="IOutputProcessor"/> after each terminal
    /// assistant response. Free-form output is a definition whose <see cref="OutputDefinition.Mode"/> is
    /// <see cref="OutputMode.Text"/>. A run completes only with an <see cref="OutputAccepted"/> decision; a rejected
    /// candidate is repaired within the definition's retry policy or the run halts with <see cref="RunPolicyHalted"/>.
    /// </value>
    /// <exception cref="ArgumentNullException">An initializer attempts to set <see langword="null"/>.</exception>
    /// <remarks>
    /// The definition owner is responsible for telling the model what to produce: in <see cref="OutputMode.Prompted"/>
    /// the schema reaches the model only through <see cref="Instructions"/>, which carry definition authority.
    /// </remarks>
    public OutputDefinition Output
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Output));
            field = value;
        }
    }

    /// <summary>Gets application-specific definition data.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ExtensionData Extensions
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Extensions));
            field = value;
        }
    }

    /// <summary>
    /// Determines whether this definition has the same identity, revision,
    /// and declarative content as <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The definition to compare, or <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> when every field is equal and the ordered
    /// instruction and toolset collections have equal contents.
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
        && Components.Equals(other.Components)
        && SessionProfile.Equals(other.SessionProfile)
        && HookProfile.Equals(other.HookProfile)
        && SecurityProfile.Equals(other.SecurityProfile)
        && OptionalCapabilities.Equals(other.OptionalCapabilities)
        && Models.Equals(other.Models)
        && Instructions.SequenceEqual(other.Instructions)
        && Toolsets.SequenceEqual(other.Toolsets)
        && RunDefaults.Equals(other.RunDefaults)
        && Output.Equals(other.Output)
        && Extensions.Equals(other.Extensions);

    /// <summary>
    /// Returns a hash code consistent with structural definition equality.
    /// </summary>
    /// <returns>
    /// A hash code over every scalar field and every ordered instruction and
    /// toolset entry.
    /// </returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Revision);
        hash.Add(DisplayName, StringComparer.Ordinal);
        hash.Add(Components);
        hash.Add(SessionProfile);
        hash.Add(HookProfile);
        hash.Add(SecurityProfile);
        hash.Add(OptionalCapabilities);
        hash.Add(Models);
        foreach (var instruction in Instructions)
        {
            hash.Add(instruction);
        }

        foreach (var toolset in Toolsets)
        {
            hash.Add(toolset);
        }

        hash.Add(RunDefaults);
        hash.Add(Output);
        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}

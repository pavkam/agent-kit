// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One immutable, versioned output contract: mode, optional schema and CLR
/// runtime type, union alternatives, ordered validator references, and
/// validation/retry policy, resolved before provider I/O.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Whether <see cref="Schema"/> is required for
/// <see cref="OutputMode.NativeSchema"/> or <see cref="OutputMode.Prompted"/>
/// is a processor-level option
/// (<c>AgentOutputOptions.RequireSchemaForStructuredModes</c>), not a
/// construction invariant of this type, so a definition can be constructed
/// before that policy is known.
/// </remarks>
public sealed record OutputDefinition
{
    /// <summary>Initializes a new instance of the <see cref="OutputDefinition"/> record.</summary>
    /// <param name="id">The identity of this definition.</param>
    /// <param name="version">The version of this definition.</param>
    /// <param name="name">A human-readable name for this definition.</param>
    /// <param name="mode">How this definition expects its terminal candidate to be produced.</param>
    /// <param name="schema">The schema a candidate must validate against, when applicable.</param>
    /// <param name="runtimeType">The CLR type to deserialize a validated candidate into, when applicable.</param>
    /// <param name="alternatives">The named schema alternatives, when <paramref name="mode"/> is <see cref="OutputMode.Union"/>.</param>
    /// <param name="validators">The additively registered validators this definition selects, in evaluation order.</param>
    /// <param name="validationPolicy">How validator failures accumulate.</param>
    /// <param name="retryPolicy">How many validation-retry attempts this definition allows.</param>
    /// <param name="endStrategy">How a response mixing output-tool and function-tool calls picks its winner.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or consists only of whitespace, or
    /// <paramref name="alternatives"/> or <paramref name="validators"/> is a default, uninitialized array.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is undefined.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="validationPolicy"/> or <paramref name="retryPolicy"/> is null.
    /// </exception>
    public OutputDefinition(
        OutputDefinitionId id,
        OutputDefinitionVersion version,
        string name,
        OutputMode mode,
        JsonSchemaDocument? schema,
        Type? runtimeType,
        ImmutableArray<OutputAlternative> alternatives,
        ImmutableArray<OutputValidatorReference> validators,
        OutputValidationPolicy validationPolicy,
        OutputRetryPolicy retryPolicy,
        OutputEndStrategy endStrategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfUndefined(mode);
        ArgumentException.ThrowIfDefault(alternatives);
        ArgumentException.ThrowIfDefault(validators);
        ArgumentNullException.ThrowIfNull(validationPolicy);
        ArgumentNullException.ThrowIfNull(retryPolicy);
        ArgumentOutOfRangeException.ThrowIfUndefined(endStrategy);

        Id = id;
        Version = version;
        Name = name;
        Mode = mode;
        Schema = schema;
        RuntimeType = runtimeType;
        Alternatives = alternatives;
        Validators = validators;
        ValidationPolicy = validationPolicy;
        RetryPolicy = retryPolicy;
        EndStrategy = endStrategy;
    }

    /// <summary>Gets the identity of this definition.</summary>
    public OutputDefinitionId Id { get; init; }

    /// <summary>Gets the version of this definition.</summary>
    public OutputDefinitionVersion Version { get; init; }

    /// <summary>Gets a human-readable name for this definition.</summary>
    public string Name { get; init; }

    /// <summary>Gets how this definition expects its terminal candidate to be produced.</summary>
    public OutputMode Mode { get; init; }

    /// <summary>Gets the schema a candidate must validate against, when applicable.</summary>
    public JsonSchemaDocument? Schema { get; init; }

    /// <summary>Gets the CLR type to deserialize a validated candidate into, when applicable.</summary>
    public Type? RuntimeType { get; init; }

    /// <summary>Gets the named schema alternatives, when <see cref="Mode"/> is <see cref="OutputMode.Union"/>.</summary>
    public ImmutableArray<OutputAlternative> Alternatives { get; init; }

    /// <summary>Gets the additively registered validators this definition selects, in evaluation order.</summary>
    public ImmutableArray<OutputValidatorReference> Validators { get; init; }

    /// <summary>Gets how validator failures accumulate.</summary>
    public OutputValidationPolicy ValidationPolicy { get; init; }

    /// <summary>Gets how many validation-retry attempts this definition allows.</summary>
    public OutputRetryPolicy RetryPolicy { get; init; }

    /// <summary>Gets how a response mixing output-tool and function-tool calls picks its winner.</summary>
    public OutputEndStrategy EndStrategy { get; init; }

    /// <inheritdoc/>
    public bool Equals(OutputDefinition? other) =>
        other is not null
        && Id.Equals(other.Id)
        && Version.Equals(other.Version)
        && Name == other.Name
        && Mode == other.Mode
        && Equals(Schema, other.Schema)
        && RuntimeType == other.RuntimeType
        && Alternatives.SequenceEqual(other.Alternatives)
        && Validators.SequenceEqual(other.Validators)
        && ValidationPolicy.Equals(other.ValidationPolicy)
        && RetryPolicy.Equals(other.RetryPolicy)
        && EndStrategy == other.EndStrategy;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Version);
        hash.Add(Name);
        hash.Add(Mode);
        hash.Add(Schema);
        hash.Add(RuntimeType);
        foreach (var alternative in Alternatives)
        {
            hash.Add(alternative);
        }

        foreach (var validator in Validators)
        {
            hash.Add(validator);
        }

        hash.Add(ValidationPolicy);
        hash.Add(RetryPolicy);
        hash.Add(EndStrategy);
        return hash.ToHashCode();
    }
}

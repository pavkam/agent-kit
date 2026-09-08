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
    /// <paramref name="name"/> is empty or consists only of whitespace;
    /// <paramref name="alternatives"/> is default or contains <see langword="null"/>;
    /// or <paramref name="validators"/> is default.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="id"/> or <paramref name="version"/> is default, or
    /// <paramref name="mode"/> or <paramref name="endStrategy"/> is undefined.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/>, <paramref name="validationPolicy"/>, or
    /// <paramref name="retryPolicy"/> is null. A default validator reference
    /// also throws this exception because it has no validator name.
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
        ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id));
        ArgumentOutOfRangeException.ThrowIfEqual(version, default, nameof(version));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfUndefined(mode);
        ArgumentException.ThrowIfContainsNull(alternatives);
        ArgumentException.ThrowIfDefault(validators);
        foreach (var validator in validators)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(validator.Name, nameof(validators));
        }

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
    /// <value>The non-default stable identity used to resolve this definition.</value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer attempts to set the default identity.</exception>
    public OutputDefinitionId Id
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(Id));
            field = value;
        }
    }

    /// <summary>Gets the version of this definition.</summary>
    /// <value>The non-default version captured with resolved output work.</value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer attempts to set the default version.</exception>
    public OutputDefinitionVersion Version
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(Version));
            field = value;
        }
    }

    /// <summary>Gets a human-readable name for this definition.</summary>
    /// <value>Non-empty display text suitable for diagnostics.</value>
    /// <exception cref="ArgumentException">An initializer attempts to set empty or whitespace-only text.</exception>
    /// <exception cref="ArgumentNullException">An initializer attempts to set <see langword="null"/>.</exception>
    public string Name
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Name));
            field = value;
        }
    }

    /// <summary>Gets how this definition expects its terminal candidate to be produced.</summary>
    /// <value>A defined output production mode.</value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer attempts to set an undefined value.</exception>
    public OutputMode Mode
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Mode));
            field = value;
        }
    }

    /// <summary>Gets the schema a candidate must validate against, when applicable.</summary>
    /// <value>The optional owned schema; mode-specific requirements are checked during profile preflight.</value>
    public JsonSchemaDocument? Schema { get; init; }

    /// <summary>Gets the CLR type to deserialize a validated candidate into, when applicable.</summary>
    /// <value>The optional application type used only after structural validation succeeds.</value>
    public Type? RuntimeType { get; init; }

    /// <summary>Gets the named schema alternatives, when <see cref="Mode"/> is <see cref="OutputMode.Union"/>.</summary>
    /// <value>An initialized immutable array containing no null alternatives.</value>
    /// <exception cref="ArgumentException">An initializer attempts to set a default array or one containing <see langword="null"/>.</exception>
    public ImmutableArray<OutputAlternative> Alternatives
    {
        get;
        init
        {
            ArgumentException.ThrowIfContainsNull(value, nameof(Alternatives));
            field = value;
        }
    }

    /// <summary>Gets the additively registered validators this definition selects, in evaluation order.</summary>
    /// <value>An initialized immutable array containing only usable validator references.</value>
    /// <exception cref="ArgumentException">An initializer attempts to set a default array.</exception>
    /// <exception cref="ArgumentNullException">
    /// An initializer supplies a default validator reference, which has no validator name.
    /// </exception>
    public ImmutableArray<OutputValidatorReference> Validators
    {
        get;
        init
        {
            ArgumentException.ThrowIfDefault(value, nameof(Validators));
            foreach (var validator in value)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(validator.Name, nameof(Validators));
            }

            field = value;
        }
    }

    /// <summary>Gets how validator failures accumulate.</summary>
    /// <value>The non-null policy applied after structural validation.</value>
    /// <exception cref="ArgumentNullException">An initializer attempts to set <see langword="null"/>.</exception>
    public OutputValidationPolicy ValidationPolicy
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ValidationPolicy));
            field = value;
        }
    }

    /// <summary>Gets how many validation-retry attempts this definition allows.</summary>
    /// <value>The non-null bounded retry policy for invalid candidates.</value>
    /// <exception cref="ArgumentNullException">An initializer attempts to set <see langword="null"/>.</exception>
    public OutputRetryPolicy RetryPolicy
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(RetryPolicy));
            field = value;
        }
    }

    /// <summary>Gets how a response mixing output-tool and function-tool calls picks its winner.</summary>
    /// <value>A defined strategy for resolving mixed terminal response content.</value>
    /// <exception cref="ArgumentOutOfRangeException">An initializer attempts to set an undefined value.</exception>
    public OutputEndStrategy EndStrategy
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(EndStrategy));
            field = value;
        }
    }

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

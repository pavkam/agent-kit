// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>
/// An immutable <see cref="IOutputDefinitionResolver"/> built once from every
/// additively registered <see cref="OutputDefinition"/>.
/// </summary>
/// <remarks>
/// This type is a thread-safe process singleton reflecting the definitions
/// registered at composition time. When a request does not specify a
/// version, the most recently registered version for that identity is
/// returned. Registering more than one definition with the same identity
/// and version fails construction.
/// </remarks>
internal sealed class InMemoryOutputDefinitionRegistry: IOutputDefinitionResolver
{
    private readonly ImmutableDictionary<OutputDefinitionId, ImmutableArray<OutputDefinition>> _definitions;
    private readonly ILogger<InMemoryOutputDefinitionRegistry> _logger;

    /// <summary>Initializes a new instance of the <see cref="InMemoryOutputDefinitionRegistry"/> class.</summary>
    /// <param name="definitions">Every additively registered output definition.</param>
    /// <param name="schemaEngine">The selected profile-specific local schema engine.</param>
    /// <param name="options">The captured processing bounds for schema preflight.</param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definitions"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="definitions"/> contains more than one definition with the same identity and version.
    /// </exception>
    public InMemoryOutputDefinitionRegistry(
        IEnumerable<OutputDefinition> definitions,
        IOutputSchemaEngine schemaEngine,
        AgentOutputOptionsSnapshot options,
        ILogger<InMemoryOutputDefinitionRegistry>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(schemaEngine);
        ArgumentNullException.ThrowIfNull(options);

        var schemaLimits = new OutputSchemaProcessingLimits(
            options.MaximumSchemaBytes,
            options.MaximumSchemaDepth,
            options.MaximumSchemaNodes);

        var builder = ImmutableDictionary.CreateBuilder<OutputDefinitionId, ImmutableArray<OutputDefinition>>();
        foreach (var definition in definitions)
        {
            PreflightDefinition(definition, schemaEngine, schemaLimits, options.RequireSchemaForStructuredModes);
            var versions = builder.TryGetValue(definition.Id, out var existing) ? existing : [];
            if (versions.Any(v => v.Version.Equals(definition.Version)))
            {
                throw new ArgumentException(
                    $"Definition '{definition.Id}' already has a registered version '{definition.Version}'.",
                    nameof(definitions));
            }

            builder[definition.Id] = versions.Add(definition);
        }

        _definitions = builder.ToImmutable();
        _logger = logger ?? NullLogger<InMemoryOutputDefinitionRegistry>.Instance;
        OutputLog.RegistryComposed(_logger, _definitions.Count, _definitions.Values.Sum(static versions => versions.Length));
    }

    private static void PreflightDefinition(
        OutputDefinition definition,
        IOutputSchemaEngine schemaEngine,
        OutputSchemaProcessingLimits limits,
        bool requireSchemaForStructuredModes)
    {
        Debug.Assert(definition is not null, "Registered definition sequences contain non-null values.");
        Debug.Assert(schemaEngine is not null, "The constructor validates its selected schema engine.");
        Debug.Assert(limits is not null, "The constructor creates validated positive schema limits.");
        if (definition.Mode == OutputMode.Text && definition.Schema is not null)
        {
            throw new OutputDefinitionConfigurationException(
                definition.Id,
                definition.Version,
                new OutputSchemaConfigurationFailure(
                    OutputSchemaConfigurationFailureKind.MalformedSchema,
                    "A JSON schema cannot be applied to plain-text output.",
                    []));
        }

        if (requireSchemaForStructuredModes
            && definition.Mode is OutputMode.NativeSchema or OutputMode.Prompted
            && definition.Schema is null)
        {
            throw new OutputDefinitionConfigurationException(
                definition.Id,
                definition.Version,
                new OutputSchemaConfigurationFailure(
                    OutputSchemaConfigurationFailureKind.MalformedSchema,
                    "The selected structured output mode requires a schema.",
                    []));
        }

        if (definition.Schema is not null)
        {
            ThrowIfRejected(definition, schemaEngine.Preflight(new OutputSchemaPreflightRequest(definition.Schema, limits)));
        }

        foreach (var alternative in definition.Alternatives)
        {
            ThrowIfRejected(definition, schemaEngine.Preflight(new OutputSchemaPreflightRequest(alternative.Schema, limits)));
        }
    }

    private static void ThrowIfRejected(OutputDefinition definition, OutputSchemaPreflightResult result)
    {
        Debug.Assert(definition is not null, "Definition preflight retains its source definition.");
        Debug.Assert(result is not null, "Schema-engine contracts return one closed non-null result.");
        if (result is OutputSchemaPreflightRejected rejected)
        {
            throw new OutputDefinitionConfigurationException(definition.Id, definition.Version, rejected.Failure);
        }
    }

    /// <inheritdoc/>
    public ValueTask<OutputDefinitionResult> ResolveAsync(
        OutputDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_definitions.TryGetValue(request.Id, out var versions) || versions.IsEmpty)
        {
            OutputLog.DefinitionResolved(_logger, request.Id, request.Version?.ToString(), "not_found");
            return ValueTask.FromResult<OutputDefinitionResult>(new OutputDefinitionNotFound(request.Id));
        }

        if (request.Version is { } version)
        {
            var match = versions.FirstOrDefault(v => v.Version.Equals(version));
            var outcome = match is not null ? "resolved" : "not_found";
            OutputLog.DefinitionResolved(_logger, request.Id, version.ToString(), outcome);
            return ValueTask.FromResult<OutputDefinitionResult>(
                match is not null ? new OutputDefinitionResolved(match) : new OutputDefinitionNotFound(request.Id));
        }

        OutputLog.DefinitionResolved(_logger, request.Id, versions[^1].Version.ToString(), "resolved_latest");
        return ValueTask.FromResult<OutputDefinitionResult>(new OutputDefinitionResolved(versions[^1]));
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Text.Json;

/// <summary>Retains a fully preflighted canonical schema and runs each validation with independent bounded state.</summary>
/// <remarks>Handles own immutable JSON evidence only. There is no disposal obligation, global registry, authority, or live DI lookup.</remarks>
internal sealed class CompiledToolSchema: ICompiledToolSchema
{
    private readonly TimeProvider _clock;
    private readonly ILogger<CompiledToolSchema> _logger;

    private CompiledToolSchema(JsonSchema schema, ToolSchemaProfile profile, ToolSchemaLimits limits, TimeProvider clock, ILogger<CompiledToolSchema> logger)
    {
        Debug.Assert(schema is not null && profile is not null && limits is not null && clock is not null && logger is not null,
            "Compile validates every collaborator before creating a fully preflighted handle.");
        Schema = schema; Profile = profile; CompilationLimits = limits; _clock = clock; _logger = logger;
    }

    /// <inheritdoc/>
    public JsonSchema Schema { get; }
    /// <inheritdoc/>
    public ToolSchemaProfile Profile { get; }
    /// <inheritdoc/>
    public ToolSchemaLimits CompilationLimits { get; }

    /// <summary>Creates a handle only after complete bounded preflight succeeds.</summary>
    /// <param name="schema">The nonnull owned canonical schema.</param>
    /// <param name="limits">The nonnull compilation bounds.</param>
    /// <param name="clock">The nonnull observation clock retained for later validation.</param>
    /// <param name="logger">The nonnull validation-category logger.</param>
    /// <param name="cancellationToken">Cancellation propagated during all local work.</param>
    /// <returns>A complete immutable handle or a content-free configuration rejection.</returns>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels during compilation.</exception>
    internal static ToolSchemaCompilationResult Compile(JsonSchema schema, ToolSchemaLimits limits,
        TimeProvider clock, ILogger<CompiledToolSchema> logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(logger);
        cancellationToken.ThrowIfCancellationRequested();
        var profile = BoundedToolSchemaEngine.DefaultProfile;
        if (schema.Dialect != profile.Dialect) { return new ToolSchemaCompilationRejected(ToolSchemaRejectionReason.UnsupportedDialect); }
        try
        {
            var processor = new ToolSchemaProcessor(limits, cancellationToken);
            var reason = processor.Preflight(schema);
            cancellationToken.ThrowIfCancellationRequested();
            return reason is { } rejected ? new ToolSchemaCompilationRejected(rejected) : new ToolSchemaCompiled(new CompiledToolSchema(schema, profile, limits, clock, logger));
        }
        catch (ToolSchemaResourceLimitException) { return new ToolSchemaCompilationRejected(ToolSchemaRejectionReason.ResourceLimitExceeded); }
        catch (InvalidOperationException) { return new ToolSchemaCompilationRejected(ToolSchemaRejectionReason.InvalidSchema); }
    }

    /// <inheritdoc/>
    public ToolSchemaValidationResult Validate(JsonElement instance, ToolSchemaLimits limits, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNotEqual(instance.ValueKind != JsonValueKind.Undefined, true, nameof(instance));
        ArgumentNullException.ThrowIfNull(limits);
        using var observation = new ToolSchemaObservation(AgentKitActivityNames.ToolSchemaValidate, _clock, _logger);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processor = new ToolSchemaProcessor(limits, cancellationToken);
            var result = processor.Validate(Schema, instance)
                ? ToolSchemaValidationResult.Valid : ToolSchemaValidationResult.Invalid;
            cancellationToken.ThrowIfCancellationRequested();
            observation.Complete(result == ToolSchemaValidationResult.Valid ? "accepted" : "invalid");
            return result;
        }
        catch (ToolSchemaResourceLimitException)
        {
            observation.Complete("resource_limited"); return ToolSchemaValidationResult.ResourceLimitExceeded;
        }
        catch (InvalidOperationException)
        {
            observation.Complete("invalid"); return ToolSchemaValidationResult.Invalid;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            observation.Complete("cancelled"); throw;
        }
    }
}

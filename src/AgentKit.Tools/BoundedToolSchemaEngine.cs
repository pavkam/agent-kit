// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Compiles the explicit bounded draft 2020-12 tool profile without provider translation, I/O, or mutable registries.</summary>
/// <remarks>The profile supports structural, exact numeric, string-length, enum, and uniqueness assertions. Unsupported keywords reject rather than disappearing. Format is annotation-only; typed semantic validators remain separate.</remarks>
internal sealed class BoundedToolSchemaEngine: IToolSchemaEngine
{
    /// <summary>The exact supported dialect; compilation additionally restricts its keyword vocabulary.</summary>
    internal const string DialectName = "https://json-schema.org/draft/2020-12/schema";
    private readonly TimeProvider _clock;
    private readonly ILogger<BoundedToolSchemaEngine> _logger;
    private readonly ILogger<CompiledToolSchema> _compiledLogger;

    /// <summary>Captures observation collaborators without performing schema work or selecting a provider.</summary>
    /// <param name="clock">The nonnull observation-only clock.</param>
    /// <param name="logger">The nonnull compiler-category logger.</param>
    /// <param name="compiledLogger">The nonnull compiled-schema-category logger retained by validation handles.</param>
    /// <exception cref="ArgumentNullException">A collaborator is null.</exception>
    internal BoundedToolSchemaEngine(TimeProvider clock, ILogger<BoundedToolSchemaEngine> logger, ILogger<CompiledToolSchema> compiledLogger)
    {
        ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(logger); ArgumentNullException.ThrowIfNull(compiledLogger);
        _clock = clock; _logger = logger; _compiledLogger = compiledLogger;
    }

    /// <inheritdoc/>
    public ToolSchemaProfile Profile => DefaultProfile;

    /// <summary>Gets the immutable capability evidence shared by this implementation and its compiled handles.</summary>
    /// <value>The exact supported dialect, keywords, and work-accounting revision.</value>
    internal static ToolSchemaProfile DefaultProfile { get; } = new(new("agentkit-bounded-tool-schema"), new(1), new(DialectName),
        ["additionalProperties", "const", "enum", "exclusiveMaximum", "exclusiveMinimum", "items", "maximum", "maxItems",
         "maxLength", "maxProperties", "minimum", "minItems", "minLength", "minProperties", "properties", "required", "type", "uniqueItems"],
        ["$comment", "default", "deprecated", "description", "examples", "format", "readOnly", "title", "writeOnly"]);

    /// <inheritdoc/>
    public ToolSchemaCompilationResult Compile(JsonSchema schema, ToolSchemaLimits limits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(limits);
        using var observation = new ToolSchemaObservation(AgentKitActivityNames.ToolSchemaCompile, _clock, _logger);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = CompiledToolSchema.Compile(schema, limits, _clock, _compiledLogger, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            observation.Complete(result is ToolSchemaCompiled ? "accepted" : "configuration_rejected");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            observation.Complete("cancelled"); throw;
        }
    }
}

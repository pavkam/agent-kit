// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

/// <summary>Validates resolved calls using the configured schema engine and bounded canonical schema rules.</summary>
public sealed class ToolArgumentValidator: IToolArgumentValidator
{
    private readonly IToolSchemaEngine _schemaEngine;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<(ToolId Id, ToolVersion Version), ToolSchemaCompilationResult> _compiledSchemas = new();

    /// <summary>Initializes the first-party argument validator.</summary>
    /// <param name="schemaEngine">The schema engine that compiles and validates tool input schemas.</param>
    /// <param name="timeProvider">The replaceable clock used for validation timestamps.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public ToolArgumentValidator(IToolSchemaEngine schemaEngine, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(schemaEngine);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _schemaEngine = schemaEngine;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public ValueTask<ToolArgumentValidationResult> ValidateAsync(
        ResolvedToolCall resolvedCall,
        ToolSchemaLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolvedCall);
        ArgumentNullException.ThrowIfNull(limits);

        JsonElement arguments;
        try
        {
            arguments = ParseRawArguments(resolvedCall.RawArguments);
        }
        catch (JsonException)
        {
            return ValueTask.FromResult<ToolArgumentValidationResult>(
                new ToolCallValidationFailed(
                    resolvedCall,
                    ToolTerminalStatus.InvalidArguments,
                    "The arguments are not valid JSON."));
        }

        var compilation = _compiledSchemas.GetOrAdd(
            (resolvedCall.Tool.Id, resolvedCall.ToolVersion),
            _ => _schemaEngine.Compile(resolvedCall.Tool.InputSchema, limits, cancellationToken));

        return compilation is not ToolSchemaCompiled compiled
            ? ValueTask.FromResult<ToolArgumentValidationResult>(
                new ToolCallValidationFailed(
                    resolvedCall,
                    ToolTerminalStatus.Unsupported,
                    "The tool's declared input schema could not be compiled by the configured validation engine."))
            : compiled.Schema.Validate(arguments, limits, cancellationToken) switch
            {
                ToolSchemaValidationResult.Valid => ValueTask.FromResult<ToolArgumentValidationResult>(
                    new ToolCallValidated(CreateValidatedCall(resolvedCall, arguments))),
                ToolSchemaValidationResult.ResourceLimitExceeded => ValueTask.FromResult<ToolArgumentValidationResult>(
                    new ToolCallValidationFailed(
                        resolvedCall,
                        ToolTerminalStatus.ResourceLimitExceeded,
                        "The arguments could not be validated within the configured resource limits.")),
                ToolSchemaValidationResult.Invalid => ValueTask.FromResult<ToolArgumentValidationResult>(
                    new ToolCallValidationFailed(
                        resolvedCall,
                        ToolTerminalStatus.InvalidArguments,
                        "The arguments did not satisfy the tool's declared schema.")),
                _ => ValueTask.FromResult<ToolArgumentValidationResult>(
                    new ToolCallValidationFailed(
                        resolvedCall,
                        ToolTerminalStatus.InvalidArguments,
                        "The arguments did not satisfy the tool's declared schema.")),
            };
    }

    private ValidatedToolCall CreateValidatedCall(ResolvedToolCall call, JsonElement arguments)
    {
        var cloned = arguments.ValueKind == JsonValueKind.Undefined
            ? JsonDocument.Parse("{}").RootElement
            : arguments.Clone();
        var fingerprint = ToolInvocationSecurityBinding.ValidatedArgumentsFingerprint(cloned);
        var validatedAt = _timeProvider.GetUtcNow();
        return new ValidatedToolCall(
            call.AgentId,
            call.SessionId,
            call.RunId,
            call.TurnId,
            call.OperationId,
            call.CallId,
            call.Authorization,
            call.CatalogVersion,
            call.ProviderAlias,
            call.Tool,
            call.ToolVersion,
            call.ExecutionPolicy,
            call.SourceOrdinal,
            cloned,
            fingerprint,
            call.RequestedAt,
            validatedAt);
    }

    private static JsonElement ParseRawArguments(ImmutableArray<byte> rawArguments)
    {
        if (rawArguments.Length == 0)
        {
            return JsonDocument.Parse("{}").RootElement;
        }

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(rawArguments.AsSpan()));
        return document.RootElement.Clone();
    }
}

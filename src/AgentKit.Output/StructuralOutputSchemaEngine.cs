// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>Provides the bounded, fail-closed AgentKit structural JSON Schema profile.</summary>
internal sealed class StructuralOutputSchemaEngine: IOutputSchemaEngine
{
    private const int _maximumSafeDepth = 128;
    private const string _dialectName = "urn:agentkit:json-schema:structural:v1";
    private static readonly ImmutableHashSet<string> _types =
        ["array", "boolean", "integer", "null", "number", "object", "string"];
    private static readonly ImmutableHashSet<string> _assertions =
        ["additionalProperties", "items", "properties", "required", "type"];
    private static readonly ImmutableHashSet<string> _annotations =
        ["$comment", "default", "deprecated", "description", "examples", "readOnly", "title", "writeOnly"];
    private static readonly JsonSchemaDialectId _dialect = new(_dialectName);
    private readonly ILogger<StructuralOutputSchemaEngine> _logger;

    /// <summary>Initializes the stateless structural engine with an optional content-free logger.</summary>
    /// <param name="logger">The logger receiving bounded operation outcomes, or null to disable logs.</param>
    public StructuralOutputSchemaEngine(ILogger<StructuralOutputSchemaEngine>? logger = null) =>
        _logger = logger ?? NullLogger<StructuralOutputSchemaEngine>.Instance;

    /// <inheritdoc/>
    public OutputSchemaEngineProfile Profile { get; } = new(
        new OutputSchemaProfileId("agentkit-structural"),
        new OutputSchemaProfileVersion(1),
        _dialect,
        [_dialect],
        [.. _assertions],
        [.. _annotations]);

    /// <inheritdoc/>
    public OutputSchemaPreflightResult Preflight(
        OutputSchemaPreflightRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(
            "preflight",
            AgentKitActivityNames.OutputSchemaPreflight,
            () => PreflightCore(request, cancellationToken),
            static result => result is OutputSchemaPreflightAccepted ? "accepted" : "configuration_rejected",
            cancellationToken);
    }

    private OutputSchemaPreflightResult PreflightCore(
        OutputSchemaPreflightRequest request,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "Public preflight validates its request before local processing.");
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Limits.MaximumDepth > _maximumSafeDepth)
        {
            return Reject(OutputSchemaConfigurationFailureKind.ResourceLimitExceeded, "The configured schema depth exceeds the engine's safe local maximum.");
        }

        if (!TryMeasure(request.Schema.Schema, request.Limits, cancellationToken, out var fingerprint, out var depth, out var nodes))
        {
            return Reject(OutputSchemaConfigurationFailureKind.ResourceLimitExceeded, "The schema exceeds the configured local processing limits.");
        }

        var dialect = _dialect;
        var failure = PreflightSchema(request.Schema.Schema, isRoot: true, "$", cancellationToken, ref dialect);
        return failure is null
            ? new OutputSchemaPreflightAccepted(
                new OutputSchemaPreflightManifest(Profile, dialect, fingerprint, request.Limits, depth, nodes))
            : new OutputSchemaPreflightRejected(failure);
    }

    /// <inheritdoc/>
    public OutputSchemaEvaluationResult Evaluate(
        OutputSchemaEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(
            "evaluate",
            AgentKitActivityNames.OutputSchemaEvaluate,
            () => EvaluateCore(request, cancellationToken),
            static result => result switch
            {
                OutputSchemaEvaluationPassed => "accepted",
                OutputSchemaCandidateInvalid => "candidate_invalid",
                _ => "configuration_rejected",
            },
            cancellationToken);
    }

    private OutputSchemaEvaluationResult EvaluateCore(
        OutputSchemaEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        Debug.Assert(request is not null, "Public evaluation validates its request before local processing.");
        cancellationToken.ThrowIfCancellationRequested();

        var preflight = PreflightCore(new OutputSchemaPreflightRequest(request.Schema, request.SchemaLimits), cancellationToken);
        if (preflight is OutputSchemaPreflightRejected rejected)
        {
            return new OutputSchemaEvaluationConfigurationRejected(rejected.Failure);
        }

        var actual = ((OutputSchemaPreflightAccepted) preflight).Manifest;
        if (!actual.Equals(request.Manifest))
        {
            return new OutputSchemaEvaluationConfigurationRejected(
                Failure(OutputSchemaConfigurationFailureKind.PreflightEvidenceMismatch, "The schema preflight evidence does not match the selected engine, schema, or limits."));
        }

        if (request.CandidateLimits.MaximumDepth > _maximumSafeDepth)
        {
            return new OutputSchemaEvaluationConfigurationRejected(
                Failure(OutputSchemaConfigurationFailureKind.ResourceLimitExceeded, "The configured candidate depth exceeds the engine's safe local maximum."));
        }

        if (!TryMeasure(request.Candidate, request.CandidateLimits, cancellationToken, out _, out _, out _))
        {
            return new OutputSchemaCandidateInvalid(
                [new OutputValidationIssue("candidate-resource-limit", "The candidate exceeds the configured local schema-evaluation limits.", null)]);
        }

        if (ContainsDuplicateObjectMembers(request.Candidate, cancellationToken))
        {
            return new OutputSchemaCandidateInvalid(
                [new OutputValidationIssue("duplicate-object-member", "The candidate contains a duplicate object member.", "$")]);
        }

        var issues = ImmutableArray.CreateBuilder<OutputValidationIssue>();
        EvaluateNode(request.Candidate, request.Schema.Schema, "$", issues, request.MaximumIssues, cancellationToken);
        return issues.Count == 0
            ? OutputSchemaEvaluationPassed.Instance
            : new OutputSchemaCandidateInvalid(issues.ToImmutable());
    }

    private TResult Observe<TResult>(
        string operation,
        string activityName,
        Func<TResult> action,
        Func<TResult, string> classify,
        CancellationToken cancellationToken)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "Schema instrumentation uses a bounded operation name.");
        Debug.Assert(!string.IsNullOrWhiteSpace(activityName), "Schema instrumentation uses a shared activity name.");
        Debug.Assert(action is not null, "Schema instrumentation requires local work.");
        Debug.Assert(classify is not null, "Schema instrumentation requires a bounded outcome classifier.");
        using var activity = AgentKitDiagnostics.Activities.StartActivity(activityName);
        try
        {
            var result = action();
            var outcome = classify(result);
            if (outcome == "accepted")
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, result?.GetType().Name ?? typeof(TResult).Name);
            }

            OutputLog.SchemaOperationCompleted(_logger, operation, outcome);
            OutputMetrics.RecordSchemaOperation(operation, outcome);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            OutputLog.SchemaOperationCancelled(_logger, operation);
            OutputMetrics.RecordSchemaOperation(operation, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            activity.SetFailed("failed", exception.GetType().FullName ?? exception.GetType().Name);
            OutputLog.SchemaOperationFailed(_logger, operation, exception.GetType().FullName ?? exception.GetType().Name);
            OutputMetrics.RecordSchemaOperation(operation, "failed");
            throw;
        }
    }

    private static OutputSchemaConfigurationFailure? PreflightSchema(
        JsonElement schema,
        bool isRoot,
        string path,
        CancellationToken cancellationToken,
        ref JsonSchemaDialectId dialect)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return null;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            return Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "A schema must be an object or boolean value.", path);
        }

        var seenKeywords = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in schema.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seenKeywords.Add(property.Name))
            {
                return Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The schema contains a duplicate keyword.", path);
            }
            if (property.NameEquals("$schema"))
            {
                if (!isRoot)
                {
                    return Failure(OutputSchemaConfigurationFailureKind.UnsupportedDialect, "Nested schema dialect switching is not supported.", path);
                }

                if (property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() is not { } value)
                {
                    return Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The schema dialect declaration must be a string.", path);
                }

                if (!StringComparer.Ordinal.Equals(value, _dialectName))
                {
                    return Failure(OutputSchemaConfigurationFailureKind.UnsupportedDialect, "The declared schema dialect is not supported by the selected engine profile.", path);
                }

                dialect = _dialect;
                continue;
            }

            if (_annotations.Contains(property.Name))
            {
                var annotationFailure = ValidateAnnotation(property.Name, property.Value, path);
                if (annotationFailure is not null)
                {
                    return annotationFailure;
                }

                continue;
            }

            if (!_assertions.Contains(property.Name))
            {
                return Failure(OutputSchemaConfigurationFailureKind.UnsupportedVocabulary, "The schema contains a keyword unsupported by the selected engine profile.", path);
            }

            var failure = property.Name switch
            {
                "type" => ValidateType(property.Value, path, cancellationToken),
                "required" => ValidateRequired(property.Value, path, cancellationToken),
                "properties" => PreflightProperties(property.Value, path, cancellationToken, ref dialect),
                "items" => PreflightSchema(property.Value, isRoot: false, path + ".items", cancellationToken, ref dialect),
                "additionalProperties" when property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False) =>
                    Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The additionalProperties assertion must be boolean in this profile.", path),
                _ => null,
            };
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private static OutputSchemaConfigurationFailure? PreflightProperties(
        JsonElement value,
        string path,
        CancellationToken cancellationToken,
        ref JsonSchemaDialectId dialect)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The properties assertion must be an object.", path);
        }

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seenNames.Add(property.Name))
            {
                return Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The properties assertion contains a duplicate member name.", path);
            }

            var failure = PreflightSchema(property.Value, isRoot: false, path + ".properties[*]", cancellationToken, ref dialect);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private static OutputSchemaConfigurationFailure? ValidateType(
        JsonElement value,
        string path,
        CancellationToken cancellationToken)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() is { } type && _types.Contains(type)
                ? null
                : Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The type assertion names an unsupported JSON type.", path);
        }

        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            return Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The type assertion must be a type name or a nonempty array of unique type names.", path);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } type || !_types.Contains(type) || !seen.Add(type))
            {
                return Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The type assertion must contain unique supported type names.", path);
            }
        }

        return null;
    }

    private static OutputSchemaConfigurationFailure? ValidateRequired(
        JsonElement value,
        string path,
        CancellationToken cancellationToken)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The required assertion must be an array of unique property names.", path);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } name || !seen.Add(name))
            {
                return Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The required assertion must contain unique string property names.", path);
            }
        }

        return null;
    }

    private static OutputSchemaConfigurationFailure? ValidateAnnotation(string name, JsonElement value, string path) => name switch
    {
        "$comment" or "title" or "description" when value.ValueKind != JsonValueKind.String =>
            Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "A textual schema annotation must be a string.", path),
        "deprecated" or "readOnly" or "writeOnly" when value.ValueKind is not (JsonValueKind.True or JsonValueKind.False) =>
            Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "A boolean schema annotation must be boolean.", path),
        "examples" when value.ValueKind != JsonValueKind.Array =>
            Failure(OutputSchemaConfigurationFailureKind.MalformedSchema, "The examples annotation must be an array.", path),
        _ => null,
    };

    private static void EvaluateNode(
        JsonElement instance,
        JsonElement schema,
        string path,
        ImmutableArray<OutputValidationIssue>.Builder issues,
        int maximumIssues,
        CancellationToken cancellationToken)
    {
        if (issues.Count == maximumIssues)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (schema.ValueKind == JsonValueKind.True)
        {
            return;
        }

        if (schema.ValueKind == JsonValueKind.False)
        {
            AddIssue(issues, maximumIssues, "false-schema", "The candidate is rejected by a false schema.", path);
            return;
        }

        if (schema.TryGetProperty("type", out var type) && !MatchesType(instance, type, cancellationToken))
        {
            AddIssue(issues, maximumIssues, "type-mismatch", "The candidate value does not match the declared type.", path);
            return;
        }

        if (instance.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
            {
                foreach (var item in required.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = item.GetString()!;
                    if (!instance.TryGetProperty(name, out _))
                    {
                        AddIssue(issues, maximumIssues, "required-property-missing", "A required property is missing.", path);
                    }
                }
            }

            var hasProperties = schema.TryGetProperty("properties", out var properties);
            if (hasProperties)
            {
                foreach (var property in properties.EnumerateObject())
                {
                    if (instance.TryGetProperty(property.Name, out var propertyValue))
                    {
                        EvaluateNode(propertyValue, property.Value, path + ".property", issues, maximumIssues, cancellationToken);
                    }
                }
            }

            if (schema.TryGetProperty("additionalProperties", out var additional)
                && additional.ValueKind == JsonValueKind.False)
            {
                foreach (var property in instance.EnumerateObject())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!hasProperties || !properties.TryGetProperty(property.Name, out _))
                    {
                        AddIssue(issues, maximumIssues, "additional-property", "The candidate contains a property not declared by the schema.", path);
                    }
                }
            }
        }
        else if (instance.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
        {
            foreach (var item in instance.EnumerateArray())
            {
                EvaluateNode(item, items, path + "[*]", issues, maximumIssues, cancellationToken);
            }
        }
    }

    private static bool MatchesType(JsonElement instance, JsonElement type, CancellationToken cancellationToken) =>
        type.ValueKind == JsonValueKind.String
            ? MatchesSingleType(instance, type.GetString()!, cancellationToken)
            : type.EnumerateArray().Any(item => MatchesSingleType(instance, item.GetString()!, cancellationToken));

    private static bool MatchesSingleType(JsonElement instance, string type, CancellationToken cancellationToken) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => instance.ValueKind == JsonValueKind.Null,
        "number" => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && IsExactInteger(instance.GetRawText(), cancellationToken),
        _ => false,
    };

    private static bool IsExactInteger(string number, CancellationToken cancellationToken)
    {
        var exponentIndex = number.IndexOfAny('e', 'E');
        var significand = exponentIndex < 0 ? number : number[..exponentIndex];
        var exponentText = exponentIndex < 0 ? "0" : number[(exponentIndex + 1)..];
        var decimalIndex = significand.IndexOf('.');
        var fractionalDigits = decimalIndex < 0 ? 0 : significand.Length - decimalIndex - 1;
        var digits = significand.Replace("-", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal);
        var exponent = ParseSaturatedExponent(exponentText, (long) fractionalDigits + digits.Length + 1, cancellationToken);
        if (exponent >= fractionalDigits)
        {
            return true;
        }

        var requiredZeros = fractionalDigits - exponent;
        if (requiredZeros > digits.Length)
        {
            return digits.All(static digit => digit == '0');
        }

        var requiredZeroCount = (int) requiredZeros;
        return digits.AsSpan(digits.Length - requiredZeroCount).IndexOfAnyExcept('0') < 0;
    }

    private static long ParseSaturatedExponent(
        string exponentText,
        long saturationMagnitude,
        CancellationToken cancellationToken)
    {
        Debug.Assert(!string.IsNullOrEmpty(exponentText), "A parsed JSON number has exponent digits when an exponent marker is present.");
        Debug.Assert(saturationMagnitude > 0, "The integer decision supplies a positive saturation threshold.");
        var negative = exponentText[0] == '-';
        var start = exponentText[0] is '-' or '+' ? 1 : 0;
        long magnitude = 0;
        for (var index = start; index < exponentText.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var digit = exponentText[index] - '0';
            Debug.Assert(digit is >= 0 and <= 9, "JsonElement preserves a syntactically valid numeric exponent.");
            if (magnitude >= saturationMagnitude
                || magnitude > (saturationMagnitude - digit) / 10)
            {
                magnitude = saturationMagnitude;
                continue;
            }

            magnitude = (magnitude * 10) + digit;
        }

        return negative ? -magnitude : magnitude;
    }

    private static bool ContainsDuplicateObjectMembers(JsonElement root, CancellationToken cancellationToken)
    {
        var pending = new Stack<JsonElement>();
        pending.Push(root);
        while (pending.TryPop(out var value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                    {
                        return true;
                    }

                    pending.Push(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    pending.Push(item);
                }
            }
        }

        return false;
    }

    private static bool TryMeasure(
        JsonElement value,
        OutputSchemaProcessingLimits limits,
        CancellationToken cancellationToken,
        out ContentHash fingerprint,
        out int observedDepth,
        out int observedNodes)
    {
        if (!TryCount(value, limits, cancellationToken, out observedDepth, out observedNodes))
        {
            fingerprint = default;
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!BoundedJsonSerializer.TryComputeHash(
                value,
                limits.MaximumUtf8Bytes,
                cancellationToken,
                out fingerprint))
        {
            fingerprint = default;
            return false;
        }

        return true;
    }

    private static bool TryCount(
        JsonElement value,
        OutputSchemaProcessingLimits limits,
        CancellationToken cancellationToken,
        out int observedDepth,
        out int observedNodes)
    {
        observedDepth = 0;
        observedNodes = 0;
        var pending = new Stack<(JsonElement Value, int Depth)>();
        pending.Push((value, 1));
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (current.Depth > limits.MaximumDepth || observedNodes == limits.MaximumNodes)
            {
                return false;
            }

            observedNodes++;
            observedDepth = Math.Max(observedDepth, current.Depth);
            if (current.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in current.Value.EnumerateObject())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (observedNodes + pending.Count >= limits.MaximumNodes)
                    {
                        return false;
                    }

                    pending.Push((property.Value, current.Depth + 1));
                }
            }
            else if (current.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in current.Value.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (observedNodes + pending.Count >= limits.MaximumNodes)
                    {
                        return false;
                    }

                    pending.Push((item, current.Depth + 1));
                }
            }
        }

        return true;
    }

    private static void AddIssue(
        ImmutableArray<OutputValidationIssue>.Builder issues,
        int maximumIssues,
        string code,
        string message,
        string path)
    {
        if (issues.Count < maximumIssues)
        {
            issues.Add(new OutputValidationIssue(code, message, path));
        }
    }

    private static OutputSchemaPreflightRejected Reject(OutputSchemaConfigurationFailureKind kind, string message) =>
        new(Failure(kind, message));

    private static OutputSchemaConfigurationFailure Failure(
        OutputSchemaConfigurationFailureKind kind,
        string message,
        string? path = null) =>
        new(kind, message, path is null ? [] : [new OutputValidationIssue("schema-configuration", message, path)]);
}

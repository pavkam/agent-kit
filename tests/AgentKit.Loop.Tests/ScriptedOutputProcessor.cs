// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>An <see cref="IOutputProcessor"/> that answers each request from a scripted queue and records what it saw.</summary>
internal sealed class ScriptedOutputProcessor: IOutputProcessor
{
    private readonly Queue<Func<OutputProcessingRequest, OutputProcessingResult>> _decisions = new();

    /// <summary>Initializes the processor with the decisions to return, in order; the last one repeats.</summary>
    /// <param name="decisions">The decision factories, each receiving the request it answers.</param>
    public ScriptedOutputProcessor(params Func<OutputProcessingRequest, OutputProcessingResult>[] decisions)
    {
        foreach (var decision in decisions)
        {
            _decisions.Enqueue(decision);
        }
    }

    /// <summary>Gets every request the processor received, in order.</summary>
    public List<OutputProcessingRequest> Requests { get; } = [];

    /// <summary>Gets or sets a gate the processor awaits before deciding, for cancellation tests.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <inheritdoc/>
    public async ValueTask<OutputProcessingResult> ProcessAsync(OutputProcessingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Add(request);
        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var decision = _decisions.Count > 1 ? _decisions.Dequeue() : _decisions.Peek();
        return decision(request);
    }

    /// <summary>Builds a minimal prompted-JSON output definition for tests.</summary>
    /// <param name="maximumRepairAttempts">The definition's retry ceiling.</param>
    /// <returns>A definition with a trivial object schema and no runtime type.</returns>
    public static OutputDefinition Definition(int maximumRepairAttempts = 2) => new(
        new OutputDefinitionId("test-output"),
        new OutputDefinitionVersion("1"),
        "test-output",
        OutputMode.Prompted,
        new JsonSchemaDocument("test-output", new SchemaVersion("1"), System.Text.Json.JsonDocument.Parse("""{"type":"object"}""").RootElement),
        runtimeType: null,
        alternatives: [],
        validators: [],
        OutputValidationPolicy.RejectOnFirstFailure,
        new OutputRetryPolicy(maximumRepairAttempts),
        OutputEndStrategy.Graceful);

    /// <summary>Builds an accepted decision carrying <paramref name="value"/>.</summary>
    /// <param name="request">The request being answered.</param>
    /// <param name="value">The deserialized value to report.</param>
    /// <returns>The accepted decision.</returns>
    public static OutputAccepted Accepted(OutputProcessingRequest request, object? value = null) => new(
        new ValidatedOutput(request.Definition.Mode, text: null, json: null, value),
        new OutputValidationManifest(request.Definition.Id, request.Definition.Mode, request.ValidationAttempt, []));

    /// <summary>Builds a retry decision with the given repair text.</summary>
    /// <param name="repairText">The safe repair instruction.</param>
    /// <returns>The retry decision.</returns>
    public static OutputRetryRequired Retry(string repairText = "Return valid JSON.") => new(
        new OutputRepairInstruction(repairText),
        new OutputValidationFailure(OutputValidationFailureKind.MalformedJson, "not json", []));

    /// <summary>Builds a terminal rejection.</summary>
    /// <returns>The rejected decision.</returns>
    public static OutputRejected Rejected() => new(
        new OutputValidationFailure(OutputValidationFailureKind.MalformedJson, "still not json", []));

    /// <summary>Builds a configuration rejection.</summary>
    /// <returns>The configuration-rejected decision.</returns>
    public static OutputConfigurationRejected ConfigurationRejected() => new(
        new OutputSchemaConfigurationFailure(OutputSchemaConfigurationFailureKind.MalformedSchema, "bad schema", []));
}

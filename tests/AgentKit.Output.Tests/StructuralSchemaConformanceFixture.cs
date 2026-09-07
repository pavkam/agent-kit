// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

using AgentKit.Conformance;

/// <summary>Composes the first-party structural schema engine through its public keyed registration.</summary>
public sealed class StructuralSchemaConformanceFixture: IOutputSchemaEngineConformanceFixture
{
    private static readonly OutputSchemaProcessingLimits _limits = new(4096, 16, 128);
    private readonly ServiceProvider _provider;
    private readonly JsonSchemaDocument _supportedSchema = TestFactory.Schema(/*lang=json,strict*/"""{"type":"string"}""");

    /// <summary>Initializes an isolated public service-provider composition.</summary>
    public StructuralSchemaConformanceFixture()
    {
        _provider = new ServiceCollection().AddAgentOutput().BuildServiceProvider(validateScopes: true);
        Engine = _provider.GetRequiredKeyedService<IOutputSchemaEngine>(AgentOutputDefaults.ProcessorKey.Value);
    }

    /// <inheritdoc/>
    public IOutputSchemaEngine Engine { get; }

    /// <inheritdoc/>
    public OutputSchemaPreflightRequest CreateSupportedPreflightRequest() => new(_supportedSchema, _limits);

    /// <inheritdoc/>
    public OutputSchemaPreflightRequest CreateUnsupportedPreflightRequest() =>
        new(TestFactory.Schema(/*lang=json,strict*/"""{"pattern":"unsupported"}"""), _limits);

    /// <inheritdoc/>
    public OutputSchemaEvaluationRequest CreateValidEvaluationRequest(OutputSchemaPreflightManifest manifest) =>
        new(_supportedSchema, TestFactory.ParseJson("\"valid\""), manifest, _limits, _limits, 4);

    /// <inheritdoc/>
    public OutputSchemaEvaluationRequest CreateInvalidEvaluationRequest(OutputSchemaPreflightManifest manifest) =>
        new(_supportedSchema, TestFactory.ParseJson("42"), manifest, _limits, _limits, 4);

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();
}

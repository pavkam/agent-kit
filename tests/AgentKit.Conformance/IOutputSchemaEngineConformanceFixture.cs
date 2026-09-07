// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates one publicly composed schema engine and portable requests for its declared profile.</summary>
/// <remarks>Each test owns and disposes a fresh fixture so adapter state and dependency-injection lifetimes cannot leak.</remarks>
public interface IOutputSchemaEngineConformanceFixture: IDisposable
{
    /// <summary>Gets the schema engine resolved through the implementation's public composition surface.</summary>
    public IOutputSchemaEngine Engine { get; }

    /// <summary>Creates a bounded schema request supported by <see cref="Engine"/>.</summary>
    public OutputSchemaPreflightRequest CreateSupportedPreflightRequest();

    /// <summary>Creates a bounded schema request that the declared profile must reject.</summary>
    public OutputSchemaPreflightRequest CreateUnsupportedPreflightRequest();

    /// <summary>Creates a valid candidate request using captured evidence from <paramref name="manifest"/>.</summary>
    /// <param name="manifest">The manifest returned for the supported schema.</param>
    public OutputSchemaEvaluationRequest CreateValidEvaluationRequest(OutputSchemaPreflightManifest manifest);

    /// <summary>Creates an invalid candidate request using captured evidence from <paramref name="manifest"/>.</summary>
    /// <param name="manifest">The manifest returned for the supported schema.</param>
    public OutputSchemaEvaluationRequest CreateInvalidEvaluationRequest(OutputSchemaPreflightManifest manifest);
}

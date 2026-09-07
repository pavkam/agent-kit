// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Declares the terminal behavior of one identified process operation without creating a host process.</summary>
public sealed record ScriptedProcessScenario
{
    /// <summary>Initializes one deterministic process scenario.</summary>
    /// <param name="operationId">The exact operation identity selecting the scenario.</param>
    /// <param name="result">The declared terminal result.</param>
    /// <param name="delayAfterStart">The non-negative injected-time delay after grant consumption.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or delay is invalid.</exception>
    public ScriptedProcessScenario(
        ProcessOperationId operationId,
        ProcessRunResult result,
        TimeSpan delayAfterStart)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationId.Value, Guid.Empty, nameof(operationId));
        ArgumentNullException.ThrowIfNull(result);
        ArgumentOutOfRangeException.ThrowIfLessThan(delayAfterStart, TimeSpan.Zero);
        OperationId = operationId;
        Result = result;
        DelayAfterStart = delayAfterStart;
    }

    /// <summary>Gets the exact operation identity selecting this scenario.</summary>
    public ProcessOperationId OperationId { get; }
    /// <summary>Gets the declared terminal result returned after the simulated delay.</summary>
    public ProcessRunResult Result { get; }
    /// <summary>Gets the injected-time delay after grant consumption.</summary>
    public TimeSpan DelayAfterStart { get; }
}

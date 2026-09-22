// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One canonical process start intent before resolution and authorization.</summary>
public sealed record ProcessStartRequest
{
    /// <summary>Initializes a structured start request.</summary>
    /// <param name="id">The process operation identity.</param>
    /// <param name="causalOperationId">The causal operation identity.</param>
    /// <param name="agentId">The owning agent identity.</param>
    /// <param name="runId">The optional active run identity.</param>
    /// <param name="executable">The configured executable reference.</param>
    /// <param name="arguments">The exact ordered arguments.</param>
    /// <param name="workingDirectory">The workspace-relative working directory target.</param>
    /// <param name="environment">The explicit environment projection.</param>
    /// <param name="standardInput">The optional standard-input payload.</param>
    /// <param name="sandboxProfileId">The required sandbox profile identity.</param>
    /// <param name="limits">The enforceable resource limits.</param>
    /// <param name="effect">The declared side-effect class.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">An immutable array is default or invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or enum is invalid.</exception>
    public ProcessStartRequest(
        ProcessOperationId id,
        OperationId causalOperationId,
        AgentId agentId,
        RunId? runId,
        ProcessExecutableReference executable,
        ImmutableArray<ProcessArgument> arguments,
        FileTarget workingDirectory,
        EnvironmentProjection environment,
        ProcessInput? standardInput,
        SandboxProfileId sandboxProfileId,
        ProcessResourceLimits limits,
        ProcessEffectClass effect)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty, nameof(id));
        ArgumentOutOfRangeException.ThrowIfEqual(causalOperationId.Value, Guid.Empty, nameof(causalOperationId));
        ArgumentOutOfRangeException.ThrowIfEqual(agentId.Value, Guid.Empty, nameof(agentId));
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentException.ThrowIfDefault(arguments);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxProfileId.Value, nameof(sandboxProfileId));
        Id = id;
        CausalOperationId = causalOperationId;
        AgentId = agentId;
        RunId = runId;
        Executable = executable;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        Environment = environment;
        StandardInput = standardInput;
        SandboxProfileId = sandboxProfileId;
        Limits = limits;
        Effect = effect;
    }

    /// <summary>Gets the process operation identity.</summary>
    public ProcessOperationId Id { get; }
    /// <summary>Gets the causal operation identity.</summary>
    public OperationId CausalOperationId { get; }
    /// <summary>Gets the owning agent identity.</summary>
    public AgentId AgentId { get; }
    /// <summary>Gets the optional active run identity.</summary>
    public RunId? RunId { get; }
    /// <summary>Gets the configured executable reference.</summary>
    public ProcessExecutableReference Executable { get; }
    /// <summary>Gets the exact ordered arguments.</summary>
    public ImmutableArray<ProcessArgument> Arguments { get; }
    /// <summary>Gets the workspace-relative working directory target.</summary>
    public FileTarget WorkingDirectory { get; }
    /// <summary>Gets the explicit environment projection.</summary>
    public EnvironmentProjection Environment { get; }
    /// <summary>Gets the optional standard-input payload.</summary>
    public ProcessInput? StandardInput { get; }
    /// <summary>Gets the required sandbox profile identity.</summary>
    public SandboxProfileId SandboxProfileId { get; }
    /// <summary>Gets the enforceable resource limits.</summary>
    public ProcessResourceLimits Limits { get; }
    /// <summary>Gets the declared side-effect class.</summary>
    public ProcessEffectClass Effect { get; }
}

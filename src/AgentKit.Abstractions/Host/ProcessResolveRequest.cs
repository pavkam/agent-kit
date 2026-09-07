// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests canonical resolution of one structured process intent before authorization.</summary>
public sealed record ProcessResolveRequest
{
    /// <summary>Initializes an unresolved structured process intent.</summary>
    /// <param name="id">The process operation identity.</param>
    /// <param name="executable">The configured executable path or resolver-owned alias.</param>
    /// <param name="arguments">The exact ordered arguments, never a joined command line.</param>
    /// <param name="workingDirectory">The canonical workspace-relative working directory, or null for the workspace root.</param>
    /// <param name="environment">The explicit non-secret environment projection.</param>
    /// <param name="standardInput">The exact standard-input bytes, or empty for immediate EOF.</param>
    /// <param name="sandboxProfile">The required sandbox profile.</param>
    /// <param name="workspaceAccess">The maximum filesystem access exposed by that sandbox.</param>
    /// <param name="sideEffectClass">The caller's declared side-effect class.</param>
    /// <param name="childPolicy">Whether child creation is denied or permitted inside the inherited sandbox.</param>
    /// <param name="limits">The enforceable process bounds.</param>
    /// <exception cref="ArgumentException">An immutable array is default, contains null, or <paramref name="executable"/> is blank or contains NUL.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity or enum is invalid.</exception>
    public ProcessResolveRequest(
        ProcessOperationId id,
        string executable,
        ImmutableArray<string> arguments,
        FileSystemPath? workingDirectory,
        ImmutableArray<ProcessEnvironmentVariable> environment,
        ImmutableArray<byte> standardInput,
        SandboxProfileId sandboxProfile,
        ProcessWorkspaceAccess workspaceAccess,
        ProcessSideEffectClass sideEffectClass,
        ProcessChildPolicy childPolicy,
        ProcessResourceLimits limits)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentException.ThrowIfContainsNul(executable);
        ArgumentException.ThrowIfDefault(arguments);
        ArgumentException.ThrowIfContainsNull(arguments);
        ArgumentException.ThrowIfDefault(environment);
        ArgumentException.ThrowIfContainsNull(environment);
        ArgumentException.ThrowIfDefault(standardInput);
        ArgumentException.ThrowIfNullOrWhiteSpace(sandboxProfile.Value, nameof(sandboxProfile));
        ArgumentOutOfRangeException.ThrowIfUndefined(workspaceAccess);
        ArgumentOutOfRangeException.ThrowIfUndefined(sideEffectClass);
        ArgumentOutOfRangeException.ThrowIfUndefined(childPolicy);
        ArgumentNullException.ThrowIfNull(limits);
        Id = id;
        Executable = executable;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        Environment = environment;
        StandardInput = standardInput;
        SandboxProfile = sandboxProfile;
        WorkspaceAccess = workspaceAccess;
        SideEffectClass = sideEffectClass;
        ChildPolicy = childPolicy;
        Limits = limits;
    }

    /// <summary>Gets the process operation identity.</summary>
    public ProcessOperationId Id { get; }
    /// <summary>Gets the configured executable reference.</summary>
    public string Executable { get; }
    /// <summary>Gets the exact ordered argument vector.</summary>
    public ImmutableArray<string> Arguments { get; }
    /// <summary>Gets the workspace-relative working directory.</summary>
    public FileSystemPath? WorkingDirectory { get; }
    /// <summary>Gets the explicit non-secret environment projection.</summary>
    public ImmutableArray<ProcessEnvironmentVariable> Environment { get; }
    /// <summary>Gets the exact standard-input bytes.</summary>
    public ImmutableArray<byte> StandardInput { get; }
    /// <summary>Gets the required sandbox profile.</summary>
    public SandboxProfileId SandboxProfile { get; }
    /// <summary>Gets the maximum workspace access exposed to the child.</summary>
    public ProcessWorkspaceAccess WorkspaceAccess { get; }
    /// <summary>Gets the caller's declared side-effect class.</summary>
    public ProcessSideEffectClass SideEffectClass { get; }
    /// <summary>Gets whether children are denied or permitted inside the inherited sandbox.</summary>
    public ProcessChildPolicy ChildPolicy { get; }
    /// <summary>Gets the enforceable duration, output, and termination bounds.</summary>
    public ProcessResourceLimits Limits { get; }
}

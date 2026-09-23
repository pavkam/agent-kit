// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Canonical sandbox construction input for one resolved process start.</summary>
public sealed record ProcessSandboxRequest
{
    /// <summary>Initializes a sandbox construction request.</summary>
    /// <param name="operationId">The process operation identity.</param>
    /// <param name="profileId">The required sandbox profile identity.</param>
    /// <param name="executable">The resolved executable facts.</param>
    /// <param name="absoluteWorkspaceRoot">The absolute workspace confinement root.</param>
    /// <param name="absoluteWorkingDirectory">The absolute child working directory.</param>
    /// <param name="workspaceAccess">The workspace access exposed inside the sandbox.</param>
    /// <param name="childPolicy">The child-process policy enforced by the sandbox.</param>
    /// <param name="environment">The explicit environment projection admitted to the sandbox.</param>
    /// <param name="arguments">The exact ordered argument vector passed to the child.</param>
    /// <param name="readOnlyToolchainRoots">Additional read-only roots captured for the child.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">A path is blank, not rooted, or an immutable array is default.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is invalid.</exception>
    public ProcessSandboxRequest(
        ProcessOperationId operationId,
        SandboxProfileId profileId,
        ResolvedExecutable executable,
        string absoluteWorkspaceRoot,
        string absoluteWorkingDirectory,
        ProcessWorkspaceAccess workspaceAccess,
        ProcessChildPolicy childPolicy,
        ImmutableArray<ProcessEnvironmentVariable> environment,
        ImmutableArray<string> arguments,
        ImmutableArray<ProcessReadOnlyRoot> readOnlyToolchainRoots)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationId.Value, Guid.Empty, nameof(operationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteWorkingDirectory);
        if (!Path.IsPathRooted(absoluteWorkspaceRoot))
        {
            throw new ArgumentException("The workspace root must be absolute.", nameof(absoluteWorkspaceRoot));
        }

        if (!Path.IsPathRooted(absoluteWorkingDirectory))
        {
            throw new ArgumentException("The working directory must be absolute.", nameof(absoluteWorkingDirectory));
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(workspaceAccess);
        ArgumentOutOfRangeException.ThrowIfUndefined(childPolicy);
        ArgumentException.ThrowIfDefault(environment);
        ArgumentException.ThrowIfContainsNull(environment);
        ArgumentException.ThrowIfDefault(arguments);
        ArgumentException.ThrowIfContainsNull(arguments);
        ArgumentException.ThrowIfDefault(readOnlyToolchainRoots);
        ArgumentException.ThrowIfContainsNull(readOnlyToolchainRoots);
        OperationId = operationId;
        ProfileId = profileId;
        Executable = executable;
        AbsoluteWorkspaceRoot = absoluteWorkspaceRoot;
        AbsoluteWorkingDirectory = absoluteWorkingDirectory;
        WorkspaceAccess = workspaceAccess;
        ChildPolicy = childPolicy;
        Environment = environment;
        Arguments = arguments;
        ReadOnlyToolchainRoots = readOnlyToolchainRoots;
    }

    /// <summary>Gets the process operation identity.</summary>
    public ProcessOperationId OperationId { get; init; }

    /// <summary>Gets the required sandbox profile identity.</summary>
    public SandboxProfileId ProfileId { get; init; }

    /// <summary>Gets the resolved executable facts.</summary>
    public ResolvedExecutable Executable { get; init; }

    /// <summary>Gets the absolute workspace confinement root.</summary>
    public string AbsoluteWorkspaceRoot { get; init; }

    /// <summary>Gets the absolute child working directory.</summary>
    public string AbsoluteWorkingDirectory { get; init; }

    /// <summary>Gets the workspace access exposed inside the sandbox.</summary>
    public ProcessWorkspaceAccess WorkspaceAccess { get; init; }

    /// <summary>Gets the child-process policy enforced by the sandbox.</summary>
    public ProcessChildPolicy ChildPolicy { get; init; }

    /// <summary>Gets the explicit environment projection admitted to the sandbox.</summary>
    public ImmutableArray<ProcessEnvironmentVariable> Environment { get; init; }

    /// <summary>Gets the exact ordered argument vector passed to the child.</summary>
    public ImmutableArray<string> Arguments { get; init; }

    /// <summary>Gets additional read-only roots captured for the child.</summary>
    public ImmutableArray<ProcessReadOnlyRoot> ReadOnlyToolchainRoots { get; init; }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolved canonical facts authorized immediately before process creation.</summary>
public sealed record ResolvedProcessStart
{
    /// <summary>Initializes a resolved start request.</summary>
    /// <param name="request">The original start request.</param>
    /// <param name="executable">The resolved executable.</param>
    /// <param name="workingDirectory">The resolved absolute working directory path.</param>
    /// <param name="environmentFingerprint">The canonical environment fingerprint.</param>
    /// <param name="standardInputFingerprint">The optional standard-input fingerprint.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">The working directory path is blank or not rooted.</exception>
    public ResolvedProcessStart(
        ProcessStartRequest request,
        ResolvedExecutable executable,
        string workingDirectory,
        ContentHash environmentFingerprint,
        ContentHash? standardInputFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (!Path.IsPathRooted(workingDirectory))
        {
            throw new ArgumentException("The resolved working directory must be absolute.", nameof(workingDirectory));
        }

        ArgumentOutOfRangeException.ThrowIfEqual(environmentFingerprint, default);
        Request = request;
        Executable = executable;
        WorkingDirectory = workingDirectory;
        EnvironmentFingerprint = environmentFingerprint;
        StandardInputFingerprint = standardInputFingerprint;
    }

    /// <summary>Gets the original start request.</summary>
    public ProcessStartRequest Request { get; init; }

    /// <summary>Gets the resolved executable.</summary>
    public ResolvedExecutable Executable { get; init; }

    /// <summary>Gets the resolved absolute working directory path.</summary>
    public string WorkingDirectory { get; init; }

    /// <summary>Gets the canonical environment fingerprint.</summary>
    public ContentHash EnvironmentFingerprint { get; init; }

    /// <summary>Gets the optional standard-input fingerprint.</summary>
    public ContentHash? StandardInputFingerprint { get; init; }
}

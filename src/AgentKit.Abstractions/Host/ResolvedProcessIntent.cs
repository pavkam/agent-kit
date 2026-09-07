// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Contains the immutable canonical process facts authorized and revalidated before creation.</summary>
public sealed record ResolvedProcessIntent
{
    /// <summary>Initializes a resolved process intent.</summary>
    /// <param name="request">The original structured request.</param>
    /// <param name="absoluteExecutablePath">The resolver-verified absolute executable path.</param>
    /// <param name="executableFingerprint">The exact executable byte fingerprint.</param>
    /// <param name="absoluteWorkspaceRoot">The canonical absolute workspace confinement root.</param>
    /// <param name="absoluteWorkingDirectory">The canonical absolute child working directory.</param>
    /// <param name="environmentFingerprint">The canonical projection fingerprint containing no raw values.</param>
    /// <param name="standardInputFingerprint">The exact standard-input fingerprint.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">An absolute path is blank or not rooted.</exception>
    public ResolvedProcessIntent(
        ProcessResolveRequest request,
        string absoluteExecutablePath,
        ContentHash executableFingerprint,
        string absoluteWorkspaceRoot,
        string absoluteWorkingDirectory,
        ContentHash environmentFingerprint,
        ContentHash standardInputFingerprint)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteWorkspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteWorkingDirectory);
        if (!Path.IsPathRooted(absoluteExecutablePath))
        {
            throw new ArgumentException("The resolved executable path must be absolute.", nameof(absoluteExecutablePath));
        }

        if (!Path.IsPathRooted(absoluteWorkspaceRoot))
        {
            throw new ArgumentException("The resolved workspace root must be absolute.", nameof(absoluteWorkspaceRoot));
        }

        if (!Path.IsPathRooted(absoluteWorkingDirectory))
        {
            throw new ArgumentException("The resolved working directory must be absolute.", nameof(absoluteWorkingDirectory));
        }

        Request = request;
        AbsoluteExecutablePath = absoluteExecutablePath;
        ExecutableFingerprint = executableFingerprint;
        AbsoluteWorkspaceRoot = absoluteWorkspaceRoot;
        AbsoluteWorkingDirectory = absoluteWorkingDirectory;
        EnvironmentFingerprint = environmentFingerprint;
        StandardInputFingerprint = standardInputFingerprint;
    }

    /// <summary>Gets the original structured request.</summary>
    public ProcessResolveRequest Request { get; }
    /// <summary>Gets the resolver-verified absolute executable path.</summary>
    public string AbsoluteExecutablePath { get; }
    /// <summary>Gets the exact executable byte fingerprint.</summary>
    public ContentHash ExecutableFingerprint { get; }
    /// <summary>Gets the canonical absolute workspace confinement root.</summary>
    public string AbsoluteWorkspaceRoot { get; }
    /// <summary>Gets the canonical absolute child working directory.</summary>
    public string AbsoluteWorkingDirectory { get; }
    /// <summary>Gets the canonical environment projection fingerprint.</summary>
    public ContentHash EnvironmentFingerprint { get; }
    /// <summary>Gets the exact standard-input fingerprint.</summary>
    public ContentHash StandardInputFingerprint { get; }
}

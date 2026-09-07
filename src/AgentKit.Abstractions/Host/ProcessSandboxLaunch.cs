// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Contains the exact trusted wrapper executable and structured arguments for a sandboxed launch.</summary>
public sealed record ProcessSandboxLaunch
{
    /// <summary>Initializes a sandbox-enforcing launch.</summary>
    /// <param name="executablePath">The absolute trusted wrapper executable.</param>
    /// <param name="arguments">The exact wrapper and child argument vector.</param>
    /// <exception cref="ArgumentException">The path is blank or not absolute, or arguments are default or contain null.</exception>
    public ProcessSandboxLaunch(string executablePath, ImmutableArray<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathRooted(executablePath))
        {
            throw new ArgumentException("The sandbox launch executable must be absolute.", nameof(executablePath));
        }

        ArgumentException.ThrowIfDefault(arguments);
        ArgumentException.ThrowIfContainsNull(arguments);
        ExecutablePath = executablePath;
        Arguments = arguments;
    }

    /// <summary>Gets the absolute trusted wrapper executable.</summary>
    public string ExecutablePath { get; }
    /// <summary>Gets the exact structured wrapper and child arguments.</summary>
    public ImmutableArray<string> Arguments { get; }
}

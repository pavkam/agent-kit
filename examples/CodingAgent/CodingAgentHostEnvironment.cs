// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Reads the explicit, non-secret process environment projected into sandboxed command execution.</summary>
internal static class CodingAgentHostEnvironment
{
    /// <summary>Returns the configured absolute read-only toolchain roots without consulting ambient command search state.</summary>
    /// <returns>Configured roots in authored order, or an empty array when no extra toolchain is exposed.</returns>
    public static ImmutableArray<string> ToolchainRoots() =>
        [.. (Environment.GetEnvironmentVariable("CODING_AGENT_TOOLCHAIN_ROOTS") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>Returns the exact PATH value projected into command processes.</summary>
    /// <returns>The configured value, or a minimal system-only search path when it is absent.</returns>
    public static string CommandPath() =>
        Environment.GetEnvironmentVariable("CODING_AGENT_COMMAND_PATH") is { Length: > 0 } configured
            ? configured
            : "/usr/bin:/bin";
}

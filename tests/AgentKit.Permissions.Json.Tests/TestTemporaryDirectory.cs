// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Creates canonical temporary directories whose path contains no replaceable ancestor link.</summary>
/// <remarks>
/// The JSON store rejects a root reached through a symbolic link, and macOS resolves the system temporary directory through
/// one. Canonicalizing the prefix keeps tests exercising the real validation instead of tripping it.
/// </remarks>
internal static class TestTemporaryDirectory
{
    /// <summary>Creates one unique canonical directory for a JSON store root.</summary>
    /// <returns>The fully qualified existing directory.</returns>
    internal static string Create()
    {
        var root = Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }

        var directory = Path.Combine(root, $"agentkit-json-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        return directory;
    }
}

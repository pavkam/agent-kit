// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

/// <summary>Creates canonical temporary directories whose path contains no replaceable ancestor link.</summary>
internal static class TestTemporaryDirectory
{
    /// <summary>Creates one unique canonical directory for a SQLite target.</summary>
    /// <returns>The fully qualified existing directory.</returns>
    internal static string Create()
    {
        var root = Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }
        var directory = Path.Combine(root, $"agentkit-sqlite-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        return directory;
    }
}

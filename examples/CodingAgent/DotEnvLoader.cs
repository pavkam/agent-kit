// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Loads a local, gitignored <c>.env.local</c> file into the process environment for local runs.</summary>
/// <remarks>
/// This exists purely so a developer can run this example without exporting <c>OPENAI_API_KEY</c> in every shell.
/// It never overrides a variable already set in the process environment, so an explicit <c>export</c> or a CI
/// secret always wins over the file. See <c>.env.example</c> for the expected format.
/// </remarks>
internal static class DotEnvLoader
{
    /// <summary>Loads <c>.env.local</c> next to this project's <c>.csproj</c>, if one exists.</summary>
    public static void LoadIfPresent()
    {
        var projectDirectory = FindProjectDirectory();
        if (projectDirectory is null)
        {
            return;
        }

        var path = Path.Combine(projectDirectory, ".env.local");
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            var value = trimmed[(separator + 1)..].Trim().Trim('"');
            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string? FindProjectDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CodingAgent.csproj")))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}

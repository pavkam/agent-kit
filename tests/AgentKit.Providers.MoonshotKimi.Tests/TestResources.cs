// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MoonshotKimi.Tests;

/// <summary>
/// Loads fixture files from this test project's <c>Resources</c> directory,
/// so simulated HTTP payloads live as real JSON files on disk rather than
/// as strings embedded in test source.
/// </summary>
internal static class TestResources
{
    private static readonly string ResourcesDirectory = ComputeResourcesDirectory();

    /// <summary>Gets the absolute path of a fixture file under <c>Resources</c>.</summary>
    /// <param name="relativePath">The fixture path, relative to the <c>Resources</c> directory.</param>
    /// <returns>The absolute path to the fixture file.</returns>
    public static string GetPath(string relativePath) => Path.Combine(ResourcesDirectory, relativePath);

    private static string ComputeResourcesDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "Resources");
}

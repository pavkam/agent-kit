// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock.Tests;

using System.Runtime.CompilerServices;

/// <summary>
/// Loads fixture files from this test project's <c>Resources</c> directory,
/// so simulated HTTP payloads live as real JSON/binary files on disk rather
/// than as strings or byte literals embedded in test source.
/// </summary>
internal static class TestResources
{
    private static readonly string ResourcesDirectory = ComputeResourcesDirectory();

    /// <summary>Gets the absolute path of a fixture file under <c>Resources</c>.</summary>
    /// <param name="relativePath">The fixture path, relative to the <c>Resources</c> directory.</param>
    /// <returns>The absolute path to the fixture file.</returns>
    public static string GetPath(string relativePath) => Path.Combine(ResourcesDirectory, relativePath);

    /// <summary>Reads a fixture file's full text content.</summary>
    /// <param name="relativePath">The fixture path, relative to the <c>Resources</c> directory.</param>
    /// <returns>The fixture file's text content.</returns>
    public static string ReadAllText(string relativePath) => File.ReadAllText(GetPath(relativePath));

    /// <summary>Reads a fixture file's raw bytes.</summary>
    /// <param name="relativePath">The fixture path, relative to the <c>Resources</c> directory.</param>
    /// <returns>The fixture file's raw bytes.</returns>
    public static byte[] ReadAllBytes(string relativePath) => File.ReadAllBytes(GetPath(relativePath));

    private static string ComputeResourcesDirectory([CallerFilePath] string thisFilePath = "") =>
        Path.Combine(Path.GetDirectoryName(thisFilePath)!, "Resources");
}

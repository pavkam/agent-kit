// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

internal static class PublicApiExtractor
{
    internal static string Generate(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        try
        {
            return assembly.GeneratePublicApi();
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"PublicApiGenerator 11.5.4 could not render the complete public API for '{assembly.GetName().Name}'. Upgrade or replace the extractor before approving snapshots.", exception);
        }
    }
}

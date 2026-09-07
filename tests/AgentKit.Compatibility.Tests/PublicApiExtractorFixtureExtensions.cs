// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

public static class PublicApiExtractorFixtureExtensions
{
    extension(IReadOnlyList<string> source)
    {
        public int PublicCount => source.Count;

        public string? PublicOrDefault(int index = 0) => index < source.Count ? source[index] : default;
    }
}

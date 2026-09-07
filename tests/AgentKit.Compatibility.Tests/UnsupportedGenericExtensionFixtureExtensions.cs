// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

public static class UnsupportedGenericExtensionFixtureExtensions
{
    extension<T>(IReadOnlyList<T> source)
        where T : notnull
    {
        public T? UnsupportedOrDefault(int index = 0) => index < source.Count ? source[index] : default;
    }
}

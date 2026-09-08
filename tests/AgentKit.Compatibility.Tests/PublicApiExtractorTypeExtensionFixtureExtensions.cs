// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Compatibility.Tests;

public static class PublicApiExtractorTypeExtensionFixtureExtensions
{
    extension(ArgumentException)
    {
        public static void ThrowIfFixture<T>(T value)
            where T : class => ArgumentNullException.ThrowIfNull(value);
    }
}

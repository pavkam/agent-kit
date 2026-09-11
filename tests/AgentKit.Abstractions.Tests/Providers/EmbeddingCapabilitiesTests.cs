// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies EmbeddingCapabilities behavior and contracts.</summary>
public sealed class EmbeddingCapabilitiesTests
{
    [Fact]
    public void EmbeddingCapabilities_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new EmbeddingCapabilities(true, true, true, true, true, null!));
        exception.ParamName.ShouldBe("extensions");
    }
}

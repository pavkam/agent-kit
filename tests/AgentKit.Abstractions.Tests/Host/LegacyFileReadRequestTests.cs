// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies LegacyFileReadRequest behavior and contracts.</summary>
public sealed class LegacyFileReadRequestTests
{
    [Fact]
    [Obsolete("Legacy host surface.")]

    public void Constructor_RoundTripsPath()
    {
        var path = new FileSystemPath("a.txt");
        var request = new LegacyFileReadRequest(path, SecurityTestData.Grant());
        request.Path.ShouldBe(path);
    }

    [Fact]
    [Obsolete("Legacy host surface.")]

    public void Equality_WhenSamePath_InstancesAreEqual()
    {
        var path = new FileSystemPath("a.txt");
        new LegacyFileReadRequest(path, SecurityTestData.Grant()).ShouldBe(new LegacyFileReadRequest(path, SecurityTestData.Grant()));
    }

    [Fact]
    [Obsolete("Legacy host surface.")]

    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LegacyFileReadRequest(new FileSystemPath("a.txt"), SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}

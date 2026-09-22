// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies spec FileReadRequest behavior and contracts.</summary>
public sealed class SpecFileReadRequestTests
{
    [Fact]
    public void Constructor_RoundTripsFields()
    {
        var request = CreateRequest();
        request.Id.Value.ShouldBe(Guid.Parse("21000000-0000-0000-0000-000000000001"));
        request.Target.Path.Value.ShouldBe("notes.txt");
        request.Bounds.MaxBytes.ShouldBe(1024);
    }

    private static FileReadRequest CreateRequest() => new(
        new FileOperationId(Guid.Parse("21000000-0000-0000-0000-000000000001")),
        new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
        new AgentId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
        runId: null,
        new FileTarget(new FileRootId("workspace"), new NormalizedRelativePath("notes.txt")),
        new FileReadBounds(1024));
}

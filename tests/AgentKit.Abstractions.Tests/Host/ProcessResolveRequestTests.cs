// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies ProcessResolveRequest behavior and contracts.</summary>
public sealed class ProcessResolveRequestTests
{
    [Fact]
    public void ProcessResolveRequest_WhenArgumentsContainNull_ThrowsExactParameter()
    {
        ImmutableArray<string> arguments = ["one", null!];
        var exception = Should.Throw<ArgumentException>(() => Request(arguments: arguments));
        exception.ParamName.ShouldBe("arguments");
    }

    [Fact]
    public void ProcessResolveRequest_WhenReadOnlyRootsAreDefault_RejectsBeforeAssignment()
    {
        var exception = Should.Throw<ArgumentException>(() => Request() with { ReadOnlyRoots = default });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ProcessResolveRequest_WhenReadOnlyRootIdentityDuplicates_RejectsBeforeAssignment()
    {
        var exception = Should.Throw<ArgumentException>(() => Request() with
        {
            ReadOnlyRoots =
            [
                new ProcessReadOnlyRoot("toolchain", "/opt/one"),
                new ProcessReadOnlyRoot("toolchain", "/opt/two"),
            ],
        });

        exception.ParamName.ShouldBe("value");
    }

    private static ProcessResolveRequest Request(ImmutableArray<string>? arguments = null, ImmutableArray<ProcessEnvironmentVariable>? environment = null, ProcessWorkspaceAccess workspaceAccess = ProcessWorkspaceAccess.ReadWrite) => new(new ProcessOperationId(Guid.Parse("11000000-0000-0000-0000-000000000001")), "/bin/sh", arguments ?? ["-lc", "sensitive command"], null, environment ?? [new ProcessEnvironmentVariable("SAFE_NAME", "sensitive-value")], [], new SandboxProfileId("workspace-no-network-v1"), workspaceAccess, ProcessSideEffectClass.WorkspaceMutation, ProcessChildPolicy.AllowSandboxed, new ProcessResourceLimits(TimeSpan.FromSeconds(10), 1024, TimeSpan.FromSeconds(1)));
}

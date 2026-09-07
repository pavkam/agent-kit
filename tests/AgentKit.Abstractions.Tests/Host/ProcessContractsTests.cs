// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

public sealed class ProcessContractsTests
{
    [Fact]
    public void ProcessResolveRequest_WhenArgumentsContainNull_ThrowsExactParameter()
    {
        ImmutableArray<string> arguments = ["one", null!];

        var exception = Should.Throw<ArgumentException>(() => Request(arguments: arguments));

        exception.ParamName.ShouldBe("arguments");
    }

    [Fact]
    public void ProcessResourceLimits_WhenTimeoutIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ProcessResourceLimits(
            TimeSpan.Zero, 100, TimeSpan.FromSeconds(1)));

        exception.ParamName.ShouldBe("timeout");
    }

    [Fact]
    public void ProcessResolutionResult_WhenStatusAndIntentDisagree_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ProcessResolutionResult(
            ProcessResolutionStatus.Resolved, null, null));

        exception.ParamName.ShouldBe("intent");
    }

    [Fact]
    public void ProcessSecurityBinding_WhenCanonicalInputChanges_ChangesEvidenceWithoutRawValues()
    {
        var baseline = Intent(Request());
        var changedArgument = Intent(Request(arguments: ["-lc", "different"]));
        var changedEnvironment = Intent(Request(environment:
            [new ProcessEnvironmentVariable("SAFE_NAME", "different-sensitive-value")]));
        var changedAccess = Intent(Request(workspaceAccess: ProcessWorkspaceAccess.ReadOnly));

        var fingerprints = new[]
        {
            ProcessSecurityBinding.Fingerprint(baseline),
            ProcessSecurityBinding.Fingerprint(changedArgument),
            ProcessSecurityBinding.Fingerprint(changedEnvironment),
            ProcessSecurityBinding.Fingerprint(changedAccess),
        };

        fingerprints.Distinct().Count().ShouldBe(fingerprints.Length);
        fingerprints.ShouldAllBe(static fingerprint =>
            !fingerprint.Value.Contains("sensitive", StringComparison.Ordinal));
        ProcessSecurityBinding.Resources(baseline).ShouldBe([
            new ProtectedResource(ProtectedResourceKind.Process, "/bin/sh"),
            new ProtectedResource(ProtectedResourceKind.Directory, "."),
            new ProtectedResource(ProtectedResourceKind.Process, "sandbox:workspace-no-network-v1"),
        ]);
    }

    [Fact]
    public void ProcessRunResult_WhenEquivalentArraysDifferByInstance_IsStructurallyEqual()
    {
        var left = new ProcessRunResult(
            ProcessRunStatus.Exited,
            0,
            [1, 2],
            [3],
            2,
            1,
            false,
            false,
            ProcessSideEffectCertainty.Completed,
            null);
        var right = new ProcessRunResult(
            ProcessRunStatus.Exited,
            0,
            [1, 2],
            [3],
            2,
            1,
            false,
            false,
            ProcessSideEffectCertainty.Completed,
            null);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void ProcessRunResult_WhenNonExitedHasExitCode_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ProcessRunResult(
            ProcessRunStatus.Denied,
            1,
            [],
            [],
            0,
            0,
            false,
            false,
            ProcessSideEffectCertainty.NotStarted,
            "Denied."));

        exception.ParamName.ShouldBe("exitCode");
    }

    private static ProcessResolveRequest Request(
        ImmutableArray<string>? arguments = null,
        ImmutableArray<ProcessEnvironmentVariable>? environment = null,
        ProcessWorkspaceAccess workspaceAccess = ProcessWorkspaceAccess.ReadWrite) => new(
            new ProcessOperationId(Guid.Parse("11000000-0000-0000-0000-000000000001")),
            "/bin/sh",
            arguments ?? ["-lc", "sensitive command"],
            null,
            environment ?? [new ProcessEnvironmentVariable("SAFE_NAME", "sensitive-value")],
            [],
            new SandboxProfileId("workspace-no-network-v1"),
            workspaceAccess,
            ProcessSideEffectClass.WorkspaceMutation,
            ProcessChildPolicy.AllowSandboxed,
            new ProcessResourceLimits(TimeSpan.FromSeconds(10), 1024, TimeSpan.FromSeconds(1)));

    private static ResolvedProcessIntent Intent(ProcessResolveRequest request) => new(
        request,
        "/bin/sh",
        new ContentHash("sha256:executable"),
        "/workspace",
        "/workspace",
        new ContentHash("sha256:environment"),
        new ContentHash("sha256:input"));
}

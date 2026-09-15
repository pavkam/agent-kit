// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies ProcessSecurityBinding behavior and contracts.</summary>
public sealed class ProcessSecurityBindingTests
{
    [Fact]
    public void ProcessSecurityBinding_WhenCanonicalInputChanges_ChangesEvidenceWithoutRawValues()
    {
        var baseline = Intent(Request());
        var changedArgument = Intent(Request(arguments: ["-lc", "different"]));
        var changedEnvironment = Intent(Request(environment: [new ProcessEnvironmentVariable("SAFE_NAME", "different-sensitive-value")]));
        var changedAccess = Intent(Request(workspaceAccess: ProcessWorkspaceAccess.ReadOnly));
        var changedReadOnlyRoots = Intent(Request() with
        {
            ReadOnlyRoots = [new ProcessReadOnlyRoot("homebrew", "/opt/homebrew")],
        });
        var fingerprints = new[]
        {
            ProcessSecurityBinding.Fingerprint(baseline),
            ProcessSecurityBinding.Fingerprint(changedArgument),
            ProcessSecurityBinding.Fingerprint(changedEnvironment),
            ProcessSecurityBinding.Fingerprint(changedAccess),
            ProcessSecurityBinding.Fingerprint(changedReadOnlyRoots),
        };
        fingerprints.Distinct().Count().ShouldBe(fingerprints.Length);
        fingerprints.ShouldAllBe(static fingerprint => !fingerprint.Value.Contains("sensitive", StringComparison.Ordinal));
        ProcessSecurityBinding.Resources(baseline).ShouldBe([new ProtectedResource(ProtectedResourceKind.Process, "/bin/sh"), new ProtectedResource(ProtectedResourceKind.Directory, "."), new ProtectedResource(ProtectedResourceKind.Process, "sandbox:workspace-no-network-v1"),]);
        ProcessSecurityBinding.Resources(changedReadOnlyRoots).ShouldContain(
            new ProtectedResource(ProtectedResourceKind.Directory, "readonly:homebrew:/opt/homebrew"));
    }

    private static ProcessResolveRequest Request(ImmutableArray<string>? arguments = null, ImmutableArray<ProcessEnvironmentVariable>? environment = null, ProcessWorkspaceAccess workspaceAccess = ProcessWorkspaceAccess.ReadWrite) => new(new ProcessOperationId(Guid.Parse("11000000-0000-0000-0000-000000000001")), "/bin/sh", arguments ?? ["-lc", "sensitive command"], null, environment ?? [new ProcessEnvironmentVariable("SAFE_NAME", "sensitive-value")], [], new SandboxProfileId("workspace-no-network-v1"), workspaceAccess, ProcessSideEffectClass.WorkspaceMutation, ProcessChildPolicy.AllowSandboxed, new ProcessResourceLimits(TimeSpan.FromSeconds(10), 1024, TimeSpan.FromSeconds(1)));
    private static ResolvedProcessIntent Intent(ProcessResolveRequest request) => new(request, "/bin/sh", new ContentHash("sha256:executable"), "/workspace", "/workspace", new ContentHash("sha256:environment"), new ContentHash("sha256:input"));
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted.Tests;



/// <summary>Verifies ScriptedProcessIntentResolver behavior and contracts.</summary>
public sealed class ScriptedProcessIntentResolverTests
{
    private static readonly ProcessOperationId _operationId = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    [Fact]
    public async Task ResolveAsync_WhenConfigured_ProducesDeterministicCanonicalEvidence()
    {
        var resolver = Resolver();
        var result = await resolver.ResolveAsync(Request(), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessResolutionStatus.Resolved);
        var intent = result.Intent.ShouldNotBeNull();
        intent.AbsoluteExecutablePath.ShouldBe("/scripted/bin/tool");
        intent.AbsoluteWorkingDirectory.ShouldBe("/scripted/workspace/src");
        intent.ExecutableFingerprint.ShouldBe(new ContentHash("sha256:tool"));
        intent.Request.Arguments.ShouldBe(["literal *", "$(never)"]);
    }

    [Fact]
    public async Task ResolveAsync_WhenEnvironmentIsNotAllowlisted_RejectsWithoutHostObservation()
    {
        var request = new ProcessResolveRequest(_operationId, "tool", [], null, [new ProcessEnvironmentVariable("PATH", "/bin")], [], new SandboxProfileId("scripted"), ProcessWorkspaceAccess.ReadOnly, ProcessSideEffectClass.ReadOnly, ProcessChildPolicy.Deny, new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero));
        var result = await Resolver().ResolveAsync(request, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessResolutionStatus.InvalidIntent);
    }

    private static ScriptedProcessIntentResolver Resolver()
    {
        var options = new ScriptedProcessOptions();
        options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool")));
        return new ScriptedProcessIntentResolver(Options.Create(options));
    }

    private static ProcessResolveRequest Request() => new(_operationId, "tool", ["literal *", "$(never)"], new FileSystemPath("src"), [], [], new SandboxProfileId("scripted"), ProcessWorkspaceAccess.ReadOnly, ProcessSideEffectClass.ReadOnly, ProcessChildPolicy.Deny, new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero));
}

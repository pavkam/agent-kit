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

    [Fact]
    public async Task ResolveAsync_WhenExecutableIsNotConfigured_RejectsIntent()
    {
        var unmatched = new ProcessResolveRequest(_operationId, "unknown-tool", [], null, [], [], new SandboxProfileId("scripted"), ProcessWorkspaceAccess.ReadOnly, ProcessSideEffectClass.ReadOnly, ProcessChildPolicy.Deny, new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero));
        var result = await Resolver().ResolveAsync(unmatched, TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessResolutionStatus.ExecutableRejected);
    }

    [Fact]
    public async Task ResolveAsync_WhenEnvironmentIsAllowlisted_OrdersItCanonicallyInResolvedEvidence()
    {
        var options = new ScriptedProcessOptions();
        options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool")));
        options.AllowedEnvironmentVariableNames.Add("A");
        options.AllowedEnvironmentVariableNames.Add("B");
        var resolver = new ScriptedProcessIntentResolver(Options.Create(options));
        var request = new ProcessResolveRequest(
            _operationId,
            "tool",
            [],
            null,
            [new ProcessEnvironmentVariable("B", "2"), new ProcessEnvironmentVariable("A", "1")],
            [],
            new SandboxProfileId("scripted"),
            ProcessWorkspaceAccess.ReadOnly,
            ProcessSideEffectClass.ReadOnly,
            ProcessChildPolicy.Deny,
            new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero));

        var result = await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessResolutionStatus.Resolved);
        var intent = result.Intent.ShouldNotBeNull();
        intent.Request.Environment.Select(static item => item.Name).ShouldBe(["A", "B"]);
    }

    [Fact]
    public void Constructor_WhenWorkspaceRootIsRelative_ThrowsArgumentException()
    {
        var options = new ScriptedProcessOptions { WorkspaceRoot = "relative/workspace" };
        _ = Should.Throw<ArgumentException>(() => new ScriptedProcessIntentResolver(Options.Create(options)));
    }

    [Fact]
    public void Constructor_WhenExecutableReferencesCollide_ThrowsArgumentException()
    {
        var options = new ScriptedProcessOptions();
        options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool")));
        options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/other", new ContentHash("sha256:other")));
        _ = Should.Throw<ArgumentException>(() => new ScriptedProcessIntentResolver(Options.Create(options)));
    }

    [Fact]
    public void Constructor_WhenEnvironmentNamesCollide_ThrowsArgumentException()
    {
        var options = new ScriptedProcessOptions();
        options.AllowedEnvironmentVariableNames.Add("A");
        options.AllowedEnvironmentVariableNames.Add("A");
        _ = Should.Throw<ArgumentException>(() => new ScriptedProcessIntentResolver(Options.Create(options)));
    }

    [Fact]
    public void Constructor_WhenAbsolutePathIsRelative_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new ScriptedExecutable("tool", "relative/path", new ContentHash("sha256:tool")));

    [Fact]
    public void Equality_WhenReferenceAbsolutePathAndFingerprintMatch_TreatsInstancesAsEqual()
    {
        var first = new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool"));
        var second = new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool"));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        (first with { }).ShouldBe(first);
    }

    private static ScriptedProcessIntentResolver Resolver()
    {
        var options = new ScriptedProcessOptions();
        options.Executables.Add(new ScriptedExecutable("tool", "/scripted/bin/tool", new ContentHash("sha256:tool")));
        return new ScriptedProcessIntentResolver(Options.Create(options));
    }

    private static ProcessResolveRequest Request() => new(_operationId, "tool", ["literal *", "$(never)"], new FileSystemPath("src"), [], [], new SandboxProfileId("scripted"), ProcessWorkspaceAccess.ReadOnly, ProcessSideEffectClass.ReadOnly, ProcessChildPolicy.Deny, new ProcessResourceLimits(TimeSpan.FromSeconds(1), 100, TimeSpan.Zero));
}

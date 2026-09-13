// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;



/// <summary>Verifies OperatingSystemProcessRunner behavior and contracts.</summary>
public sealed class OperatingSystemProcessRunnerTests: IDisposable
{
    public OperatingSystemProcessRunnerTests() => _ = Directory.CreateDirectory(_root);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"agentkit-process-{Guid.NewGuid():N}");
    [Fact]
    public void Constructor_WhenLegacyLoggerAndArtifactArgumentsAreNull_RetainsUnambiguousSourceCompatibility()
    {
        using var runner = new OperatingSystemProcessRunner(CreateResolver("/bin/sh"), [new PlatformProcessSandboxProvider()], new TestGrantStore(), TimeProvider.System, Options.Create(OptionsFor("/bin/sh", 1024)), null, null);
    }

    [Fact]
    public void Constructor_WhenIntentIdsNull_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new OperatingSystemProcessRunner(CreateResolver("/bin/sh"), [new PlatformProcessSandboxProvider()], new TestGrantStore(), TimeProvider.System, Options.Create(OptionsFor("/bin/sh", 1024)), null, null, null!));
        exception.ParamName.ShouldBe("intentIds");
    }

    [Fact]
    public async Task RunAsync_WhenGrantDenied_StartsNothingAndUsesExactEnforcement()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh");
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "touch denied.txt"]), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Unknown
        };
        using var runner = CreateRunner(resolver, store);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Denied);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
        File.Exists(Path.Combine(_root, "denied.txt")).ShouldBeFalse();
        var enforcement = store.Enforcements.ShouldHaveSingleItem();
        enforcement.Kind.ShouldBe(SecurityOperationKind.Process);
        enforcement.Effect.ShouldBe(SecurityEffect.Execute);
        enforcement.Resources.ShouldBe(ProcessSecurityBinding.Resources(intent));
        enforcement.InputFingerprint.ShouldBe(ProcessSecurityBinding.Fingerprint(intent));
    }

    [Fact]
    public async Task RunAsync_WhenStoreReconcilesAnEarlierIntent_DoesNotCreateTheProcess()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh");
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "touch reconciled.txt"]), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore
        {
            Status = GrantConsumptionStatus.Reconciled
        };
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("80000000-0000-0000-0000-000000000008"));
        using var runner = CreateRunner(resolver, store, intentIds: new SequenceSecurityEnforcementIntentIdGenerator(expectedId.Value));
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Denied);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
        File.Exists(Path.Combine(_root, "reconciled.txt")).ShouldBeFalse();
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
        store.LegacyConsumptionCalls.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WhenConsumedResultLacksExactReceipt_DoesNotCreateTheProcess()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh");
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "touch receipt-missing.txt"]), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore
        {
            IncludeIntentReceipt = false
        };
        using var runner = CreateRunner(resolver, store);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Denied);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
        File.Exists(Path.Combine(_root, "receipt-missing.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_WhenCapturedGrantIsRegistered_ConsumesItsExactAuthorizationEvidence()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/echo");
        var intent = (await resolver.ResolveAsync(Request("/bin/echo", ["captured"]), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new InMemorySecurityGrantStore(TimeProvider.System);
        var grant = CapturedGrantFactory.Create(new ComponentId("agentkit.processes.operating-system"), ProcessSecurityBinding.Resources(intent), ProcessSecurityBinding.Fingerprint(intent));
        await store.RegisterAsync(grant, TestContext.Current.CancellationToken);
        using var runner = CreateRunner(resolver, store);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, grant), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Exited);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.Completed);
    }

    [Fact]
    public async Task RunAsync_WhenCallerCancelsDuringNonCooperativeConsumption_DoesNotCreateTheProcess()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh");
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "touch cancelled-before-create.txt"]), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        using var cancellation = new CancellationTokenSource();
        var store = new TestGrantStore
        {
            OnIntentConsumption = cancellation.Cancel
        };
        using var runner = CreateRunner(resolver, store);
        var action = async () => await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        _ = store.Intents.ShouldHaveSingleItem();
        File.Exists(Path.Combine(_root, "cancelled-before-create.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_WhenArgumentsContainShellSyntax_PassesThemLiterallyWithoutInterpretation()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/echo");
        var intent = (await resolver.ResolveAsync(Request("/bin/echo", ["$(touch hacked.txt)"], workspaceAccess: ProcessWorkspaceAccess.ReadWrite), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        using var runner = CreateRunner(resolver, new TestGrantStore());
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Exited);
        result.ExitCode.ShouldBe(0);
        Encoding.UTF8.GetString(result.StandardOutputTail.AsSpan()).ShouldBe("$(touch hacked.txt)\n");
        File.Exists(Path.Combine(_root, "hacked.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_WhenSandboxProfileIsMissing_DoesNotConsumeGrantOrStart()
    {
        if (!IsSupported())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh");
        var unresolved = Request("/bin/sh", [], sandboxProfile: new SandboxProfileId("missing"));
        var intent = (await resolver.ResolveAsync(unresolved, TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore();
        using var runner = CreateRunner(resolver, store);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.SandboxUnavailable);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
        store.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenExecutableBytesChangeAfterAuthorization_RejectsBeforeGrantConsumption()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var executable = Path.Combine(_root, "fixture.sh");
        await File.WriteAllTextAsync(executable, "#!/bin/sh\nprintf first\n", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var resolver = CreateResolver(executable);
        var intent = (await resolver.ResolveAsync(Request(executable, []), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        await File.WriteAllTextAsync(executable, "#!/bin/sh\nprintf changed\n", TestContext.Current.CancellationToken);
        var store = new TestGrantStore();
        using var runner = CreateRunner(resolver, store);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.ResolutionFailed);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.NotStarted);
        store.Enforcements.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenOutputExceedsBound_RetainsExactTailPerStream()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh", maximumOutputBytes: 4);
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "printf 0123456789"], maximumOutputBytes: 4), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        using var runner = CreateRunner(resolver, new TestGrantStore(), maximumOutputBytes: 4);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Exited);
        result.TotalStandardOutputBytes.ShouldBe(10);
        result.StandardOutputTruncated.ShouldBeTrue();
        Encoding.UTF8.GetString(result.StandardOutputTail.AsSpan()).ShouldBe("6789");
    }

    [Fact]
    public async Task RunAsync_WhenOutputTailTruncates_PreservesCompleteStreamAsArtifact()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh", maximumOutputBytes: 4);
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "printf 0123456789"], maximumOutputBytes: 4), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var artifacts = new RecordingProcessOutputArtifactSink();
        using var runner = CreateRunner(resolver, new TestGrantStore(), maximumOutputBytes: 4, outputArtifacts: artifacts);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.StandardOutputArtifact.ShouldBeSameAs(artifacts.Reference);
        var stored = artifacts.Requests.ShouldHaveSingleItem();
        stored.Kind.ShouldBe(ProcessOutputKind.StandardOutput);
        Encoding.UTF8.GetString(stored.Content.AsSpan()).ShouldBe("0123456789");
    }

    [Fact]
    public async Task RunAsync_WhenCompleteOutputExceedsArtifactBound_ReportsLossWithoutCallingSink()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh", maximumOutputBytes: 4);
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "printf 0123456789"], maximumOutputBytes: 4), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var artifacts = new RecordingProcessOutputArtifactSink();
        using var runner = CreateRunner(resolver, new TestGrantStore(), maximumOutputBytes: 4, maximumArtifactOutputBytes: 5, outputArtifacts: artifacts);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Exited);
        result.StandardOutputArtifact.ShouldBeNull();
        result.SafeMessage.ShouldNotBeNull().ShouldContain("could not be preserved completely");
        artifacts.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenBothStreamsProduceOutput_PreservesTheirIdentity()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh");
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "printf out; printf err >&2"]), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        using var runner = CreateRunner(resolver, new TestGrantStore());
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Exited);
        Encoding.UTF8.GetString(result.StandardOutputTail.AsSpan()).ShouldBe("out");
        Encoding.UTF8.GetString(result.StandardErrorTail.AsSpan()).ShouldBe("err");
    }

    [Fact]
    public async Task RunAsync_WhenWorkspaceIsReadOnly_SandboxPreventsMutation()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh");
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "printf blocked > should-not-exist.txt"], workspaceAccess: ProcessWorkspaceAccess.ReadOnly, sideEffectClass: ProcessSideEffectClass.ReadOnly), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        using var runner = CreateRunner(resolver, new TestGrantStore());
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.Exited);
        result.ExitCode.ShouldNotBe(0);
        File.Exists(Path.Combine(_root, "should-not-exist.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_WhenChildProcessesDenied_EnforcesOrFailsClosedBeforeGrant()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh");
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "/usr/bin/touch child-created.txt"], childPolicy: ProcessChildPolicy.Deny), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        var store = new TestGrantStore();
        using var runner = CreateRunner(resolver, store);
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        File.Exists(Path.Combine(_root, "child-created.txt")).ShouldBeFalse();
        if (OperatingSystem.IsLinux())
        {
            result.Status.ShouldBe(ProcessRunStatus.SandboxUnavailable);
            store.Enforcements.ShouldBeEmpty();
        }
        else
        {
            result.Status.ShouldBe(ProcessRunStatus.Exited);
            result.ExitCode.ShouldNotBe(0);
            _ = store.Enforcements.ShouldHaveSingleItem();
        }
    }

    [Fact]
    public async Task RunAsync_WhenTimeoutElapses_TerminatesOwnedProcessAndReportsPossibleEffects()
    {
        if (!IsSupported() || !SandboxAvailable())
        {
            return;
        }

        var resolver = CreateResolver("/bin/sh");
        var intent = (await resolver.ResolveAsync(Request("/bin/sh", ["-c", "sleep 5"], timeout: TimeSpan.FromMilliseconds(100), grace: TimeSpan.FromMilliseconds(50)), TestContext.Current.CancellationToken)).Intent.ShouldNotBeNull();
        using var runner = CreateRunner(resolver, new TestGrantStore());
        var result = await runner.RunAsync(new ProcessRunRequest(intent, TestGrantStore.Grant()), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProcessRunStatus.TimedOut);
        result.EffectCertainty.ShouldBe(ProcessSideEffectCertainty.MayHaveOccurred);
    }

    private OperatingSystemProcessIntentResolver CreateResolver(string executable, long maximumOutputBytes = 1024) => new(Options.Create(OptionsFor(executable, maximumOutputBytes)));
    private OperatingSystemProcessRunner CreateRunner(IProcessIntentResolver resolver, ISecurityGrantStore store, long maximumOutputBytes = 1024, long maximumArtifactOutputBytes = 64 * 1024 * 1024, IProcessOutputArtifactSink? outputArtifacts = null, IIdentifierGenerator<SecurityEnforcementIntentId>? intentIds = null) => intentIds is null ? new OperatingSystemProcessRunner(resolver, [new PlatformProcessSandboxProvider()], store, TimeProvider.System, Options.Create(OptionsFor("/bin/sh", maximumOutputBytes, maximumArtifactOutputBytes)), outputArtifacts: outputArtifacts) : new OperatingSystemProcessRunner(resolver, [new PlatformProcessSandboxProvider()], store, TimeProvider.System, Options.Create(OptionsFor("/bin/sh", maximumOutputBytes, maximumArtifactOutputBytes)), null, outputArtifacts, intentIds);
    private OperatingSystemProcessOptions OptionsFor(string executable, long maximumOutputBytes, long maximumArtifactOutputBytes = 64 * 1024 * 1024)
    {
        var options = new OperatingSystemProcessOptions
        {
            RootDirectory = _root,
            MaximumOutputBytes = maximumOutputBytes,
            MaximumArtifactOutputBytes = maximumArtifactOutputBytes,
            MaximumTimeout = TimeSpan.FromSeconds(10),
        };
        options.AllowedExecutablePaths.Add(executable);
        return options;
    }

    private static ProcessResolveRequest Request(string executable, ImmutableArray<string> arguments, FileSystemPath? workingDirectory = null, ProcessWorkspaceAccess workspaceAccess = ProcessWorkspaceAccess.ReadWrite, ProcessSideEffectClass sideEffectClass = ProcessSideEffectClass.WorkspaceMutation, long maximumOutputBytes = 1024, TimeSpan? timeout = null, TimeSpan? grace = null, ImmutableArray<ProcessEnvironmentVariable> environment = default, SandboxProfileId? sandboxProfile = null, ProcessChildPolicy childPolicy = ProcessChildPolicy.AllowSandboxed) => new(new ProcessOperationId(Guid.NewGuid()), executable, arguments, workingDirectory, environment.IsDefault ? [] : environment, [], sandboxProfile ?? PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile, workspaceAccess, sideEffectClass, childPolicy, new ProcessResourceLimits(timeout ?? TimeSpan.FromSeconds(2), maximumOutputBytes, grace ?? TimeSpan.FromMilliseconds(250)));
    private static bool IsSupported() => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    private static bool SandboxAvailable() => OperatingSystem.IsMacOS() ? File.Exists("/usr/bin/sandbox-exec") : File.Exists("/usr/bin/bwrap");
    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}

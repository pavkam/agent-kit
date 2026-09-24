// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command.Tests;

using AgentKit.TestSupport;

public sealed class CommandToolTests
{
    [Fact]
    public void CommandToolOptions_WhenDefaulted_UsesNonLoginShellAndEmptyEnvironment()
    {
        var options = new CommandToolOptions();

        options.ShellArguments.ShouldBe(["-c"]);
        options.EnvironmentVariables.ShouldBeEmpty();
        options.ProcessExecutorKey.Value.ShouldBe("default");
    }

    [Theory]
    [InlineData(/*lang=json,strict*/ "{}")]
    [InlineData(/*lang=json,strict*/ "{\"command\":1}")]
    [InlineData(/*lang=json,strict*/ "{\"command\":\"x\",\"working_directory\":\"../escape\"}")]
    [InlineData(/*lang=json,strict*/ "{\"command\":\"x\",\"workspace_access\":\"surprise\"}")]
    [InlineData(/*lang=json,strict*/ "{\"command\":\"x\",\"timeout_ms\":600001}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_PerformsNoResolutionAuthorizationOrExecution(string json)
    {
        var resolver = new RecordingExecutableResolver();
        var executor = new RecordingProcessExecutor();
        var authority = new RecordingSecurityAuthority();

        var result = await CreateTool(resolver, executor, authority).InvokeAsync(
            Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Rejected);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        resolver.Requests.ShouldBeEmpty();
        authority.Requests.ShouldBeEmpty();
        executor.Starts.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenCommandContainsNul_RejectsWithoutResolution()
    {
        var resolver = new RecordingExecutableResolver();
        var json = JsonSerializer.Serialize(new { command = "echo\0hi" });

        var result = await CreateTool(resolver, new RecordingProcessExecutor(), new RecordingSecurityAuthority()).InvokeAsync(
            Request(json), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.FailureReason.ShouldBe("The command contains NUL or exceeds the configured byte boundary.");
        resolver.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenCommandExceedsMaximumBytes_RejectsWithoutResolution()
    {
        var resolver = new RecordingExecutableResolver();
        var options = OptionsForTool();
        options.MaximumCommandBytes = 4;
        var json = JsonSerializer.Serialize(new { command = "a very long command" });

        var result = await CreateTool(resolver, new RecordingProcessExecutor(), new RecordingSecurityAuthority(), options).InvokeAsync(
            Request(json), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvalidArguments);
        result.Outcome.FailureReason.ShouldBe("The command contains NUL or exceeds the configured byte boundary.");
        resolver.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenResolutionFails_DoesNotRequestAuthorityOrExecute()
    {
        var resolver = new RecordingExecutableResolver
        {
            Result = new ExecutableResolutionFailed("Working directory rejected."),
        };
        var executor = new RecordingProcessExecutor();
        var authority = new RecordingSecurityAuthority();

        var result = await CreateTool(resolver, executor, authority).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"pwd"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Working directory rejected.");
        authority.Requests.ShouldBeEmpty();
        executor.Starts.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorityDenies_DoesNotExecuteResolvedProcess()
    {
        var resolver = new RecordingExecutableResolver();
        var executor = new RecordingProcessExecutor();

        var result = await CreateTool(resolver, executor, new RecordingSecurityAuthority(allow: false)).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"touch nope"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Denied.");
        _ = resolver.Requests.ShouldHaveSingleItem();
        executor.Starts.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSuccessful_UsesExplicitShellAndExactResolvedSecurityEvidence()
    {
        var resolver = new RecordingExecutableResolver();
        var handle = new TestProcessHandle
        {
            StandardOutput = [.. "out"u8],
            StandardError = [.. "warn"u8],
        };
        var executor = new RecordingProcessExecutor { Result = new ProcessHandleStarted(handle) };
        var authority = new RecordingSecurityAuthority();

        var result = await CreateTool(resolver, executor, authority).InvokeAsync(
            Request(
                /*lang=json,strict*/
                """{"command":"printf '%s' hi","working_directory":"src","workspace_access":"read_only","timeout_ms":500,"maximum_output_bytes":12}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var startRequest = resolver.Requests.ShouldHaveSingleItem();
        startRequest.Executable.Value.ShouldBe("/configured/shell");
        startRequest.Arguments.Select(static argument => argument.Value).ShouldBe(["--fixed", "printf '%s' hi"]);
        startRequest.WorkingDirectory.Path.ShouldBe(new NormalizedRelativePath("src"));
        startRequest.Effect.ShouldBe(ProcessEffectClass.ReadOnlyObservation);
        startRequest.Environment.Variables.ShouldBe(
            [new ProcessEnvironmentVariable("LANG", "C.UTF-8"), new ProcessEnvironmentVariable("PATH", "/toolchain/bin")]);
        startRequest.Limits.Timeout.ShouldBe(TimeSpan.FromMilliseconds(500));
        startRequest.Limits.MaximumOutputBytes.ShouldBe(12);

        var host = executor.Starts.ShouldHaveSingleItem();
        var intent = ProcessStartBinding.ToResolvedProcessIntent(host.Request);
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Audience.ShouldBe(executor.SecurityAudience);
        security.Kind.ShouldBe(SecurityOperationKind.Process);
        security.Effect.ShouldBe(SecurityEffect.Execute);
        security.Resources.ShouldBe(ProcessSecurityBinding.Resources(intent));
        security.InputFingerprint.ShouldBe(ProcessSecurityBinding.Fingerprint(intent));
        host.Grant.RequestId.ShouldBe(security.Id);

        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("stdout").GetString().ShouldBe("out");
        json.RootElement.GetProperty("stderr").GetString().ShouldBe("warn");
        json.RootElement.GetProperty("effect_certainty").GetString().ShouldBe("DefinitelyPerformed");
    }

    [Fact]
    public async Task InvokeAsync_WhenExitCodeNonzero_ReturnsFailedOutcomeWithTypedOutput()
    {
        var handle = new TestProcessHandle
        {
            Exit = new ProcessExited(7, SideEffectCertainty.DefinitelyPerformed),
            StandardError = [.. "bad"u8],
        };
        var executor = new RecordingProcessExecutor { Result = new ProcessHandleStarted(handle) };

        var result = await CreateTool(
            new RecordingExecutableResolver(), executor, new RecordingSecurityAuthority()).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"false"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason.ShouldBe("The command exited with code 7.");
        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("exit_code").GetInt32().ShouldBe(7);
        json.RootElement.GetProperty("stderr").GetString().ShouldBe("bad");
    }

    [Fact]
    public async Task InvokeAsync_WhenOutputIsNotUtf8_PreservesExactBase64WithoutLossyText()
    {
        var handle = new TestProcessHandle { StandardOutput = [0xff, 0x00] };
        var executor = new RecordingProcessExecutor { Result = new ProcessHandleStarted(handle) };

        var result = await CreateTool(
            new RecordingExecutableResolver(), executor, new RecordingSecurityAuthority()).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"binary"}"""),
            TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("stdout").ValueKind.ShouldBe(JsonValueKind.Null);
        json.RootElement.GetProperty("stdout_base64").GetString().ShouldBe("/wA=");
        json.RootElement.GetProperty("stdout_valid_utf8").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenOutputTruncated_ProjectsTruncationFlagsWithoutArtifacts()
    {
        var handle = new TestProcessHandle
        {
            StandardOutput = [.. "tail"u8],
        };
        var executor = new RecordingProcessExecutor { Result = new ProcessHandleStarted(handle) };

        var result = await CreateTool(
            new RecordingExecutableResolver(), executor, new RecordingSecurityAuthority()).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"large","maximum_output_bytes":2}"""),
            TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("stdout_truncated").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("stdout_artifact_id").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed)]
    [InlineData(ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed, ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed)]
    [InlineData(ToolTerminalStatus.Unsupported, SideEffectCertainty.DefinitelyNotPerformed, ToolTerminalStatus.Unsupported, SideEffectCertainty.DefinitelyNotPerformed)]
    [InlineData(ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.DefinitelyNotPerformed)]
    [InlineData(ToolTerminalStatus.TimedOut, SideEffectCertainty.Unknown, ToolTerminalStatus.TimedOut, SideEffectCertainty.Unknown)]
    [InlineData(ToolTerminalStatus.Cancelled, SideEffectCertainty.Unknown, ToolTerminalStatus.Cancelled, SideEffectCertainty.Unknown)]
    public async Task InvokeAsync_WhenStartSettles_PreservesStageAndEffectEvidence(
        ToolTerminalStatus startStatus,
        SideEffectCertainty hostCertainty,
        ToolTerminalStatus expectedStatus,
        SideEffectCertainty expectedCertainty)
    {
        var executor = new RecordingProcessExecutor
        {
            Result = startStatus switch
            {
                ToolTerminalStatus.Succeeded => new ProcessHandleStarted(new TestProcessHandle()),
                ToolTerminalStatus.Denied => new ProcessStartDenied("Host settlement."),
                ToolTerminalStatus.Unsupported => new ProcessStartSandboxUnavailable("Host settlement."),
                ToolTerminalStatus.InvocationFailed => new ProcessStartFailed("Host settlement."),
                ToolTerminalStatus.TimedOut => new ProcessHandleStarted(new TestProcessHandle
                {
                    Exit = new ProcessTimedOut(hostCertainty),
                }),
                ToolTerminalStatus.Cancelled => new ProcessHandleStarted(new TestProcessHandle
                {
                    Exit = new ProcessCancelled(hostCertainty),
                }),
                ToolTerminalStatus.UnknownTool => throw new NotImplementedException(),
                ToolTerminalStatus.InvalidArguments => throw new NotImplementedException(),
                ToolTerminalStatus.ApprovalDenied => throw new NotImplementedException(),
                ToolTerminalStatus.ApprovalExpired => throw new NotImplementedException(),
                ToolTerminalStatus.Interrupted => throw new NotImplementedException(),
                ToolTerminalStatus.ResultNormalizationFailed => throw new NotImplementedException(),
                ToolTerminalStatus.ResultSerializationFailed => throw new NotImplementedException(),
                ToolTerminalStatus.ProtocolFailed => throw new NotImplementedException(),
                ToolTerminalStatus.ResourceLimitExceeded => throw new NotImplementedException(),
                _ => new ProcessStartFailed("Host settlement."),
            },
        };

        var result = await CreateTool(new RecordingExecutableResolver(), executor, new RecordingSecurityAuthority()).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"work"}"""), TestContext.Current.CancellationToken);

        result.Outcome.SourceStatus.ShouldBe(expectedStatus);
        result.Outcome.SideEffectCertainty.ShouldBe(expectedCertainty);
        result.Outcome.Retryable.ShouldBeFalse();
        if (startStatus is ToolTerminalStatus.Succeeded or ToolTerminalStatus.TimedOut or ToolTerminalStatus.Cancelled)
        {
            _ = executor.Starts.ShouldHaveSingleItem();
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenResolutionDoesNotProduceFacts_PreservesStageWithoutStartingProcess()
    {
        var resolver = new RecordingExecutableResolver { Result = new ExecutableResolutionFailed("Unavailable.") };
        var executor = new RecordingProcessExecutor();
        var authority = new RecordingSecurityAuthority();
        var result = await CreateTool(resolver, executor, authority).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"work"}"""), TestContext.Current.CancellationToken);
        result.Outcome.SourceStatus.ShouldBe(ToolTerminalStatus.InvocationFailed);
        result.Outcome.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
        result.Outcome.Retryable.ShouldBeFalse();
        authority.Requests.ShouldBeEmpty();
        executor.Starts.ShouldBeEmpty();
    }

    [Fact]
    public void Descriptor_WhenAccessed_MatchesPresentationDescriptor()
    {
        _ = CreateTool(new RecordingExecutableResolver(), new RecordingProcessExecutor(), new RecordingSecurityAuthority());

        CommandTool.Descriptor.ShouldBeSameAs(CommandTool.PresentationDescriptor);
    }

    private static CommandTool CreateTool(
        RecordingExecutableResolver resolver,
        RecordingProcessExecutor executor,
        ISecurityAuthority authority,
        CommandToolOptions? options = null)
    {
        var selector = new FixedProcessExecutorSelector(resolver, executor);
        return new CommandTool(
            selector,
            new FixedSecurityAuthoritySelector(authority),
            new FixedSecurityRequestIdGenerator(),
            new FixedProcessOperationIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(options ?? OptionsForTool()));
    }

    private static CommandToolOptions OptionsForTool()
    {
        var options = new CommandToolOptions
        {
            ShellExecutable = "/configured/shell",
            DefaultTimeout = TimeSpan.FromSeconds(30),
            MaximumTimeout = TimeSpan.FromMinutes(10),
            ProcessExecutorKey = new ProcessExecutorKey("test"),
        };
        options.ShellArguments.Clear();
        options.ShellArguments.Add("--fixed");
        options.EnvironmentVariables.Add("PATH", "/toolchain/bin");
        options.EnvironmentVariables.Add("LANG", "C.UTF-8");
        return options;
    }

    private static ToolInvocationContext Request(string json) => ToolCaptureTestData.FromLegacyRequest(new(
        TestSecurityEvidence.ToolContext(
            new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new SessionId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                new RunId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                null),
            TestExecutionIdentity.Create(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch), CommandTool.Descriptor);
}

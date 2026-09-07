// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command.Tests;

public sealed class CommandToolTests
{
    [Theory]
    [InlineData(/*lang=json,strict*/ "{}")]
    [InlineData(/*lang=json,strict*/ "{\"command\":1}")]
    [InlineData(/*lang=json,strict*/ "{\"command\":\"x\",\"working_directory\":\"../escape\"}")]
    [InlineData(/*lang=json,strict*/ "{\"command\":\"x\",\"workspace_access\":\"surprise\"}")]
    [InlineData(/*lang=json,strict*/ "{\"command\":\"x\",\"timeout_ms\":600001}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_PerformsNoResolutionAuthorizationOrExecution(string json)
    {
        var resolver = new RecordingProcessResolver();
        var runner = new RecordingProcessRunner();
        var authority = new RecordingSecurityAuthority();

        var result = await CreateTool(resolver, runner, authority).InvokeAsync(
            Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        resolver.Requests.ShouldBeEmpty();
        authority.Requests.ShouldBeEmpty();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenResolutionFails_DoesNotRequestAuthorityOrExecute()
    {
        var resolver = new RecordingProcessResolver
        {
            Result = new ProcessResolutionResult(
                ProcessResolutionStatus.WorkingDirectoryRejected,
                null,
                "Working directory rejected."),
        };
        var runner = new RecordingProcessRunner();
        var authority = new RecordingSecurityAuthority();

        var result = await CreateTool(resolver, runner, authority).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"pwd"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Working directory rejected.");
        authority.Requests.ShouldBeEmpty();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorityDenies_DoesNotExecuteResolvedProcess()
    {
        var resolver = new RecordingProcessResolver();
        var runner = new RecordingProcessRunner();

        var result = await CreateTool(resolver, runner, new RecordingSecurityAuthority(allow: false)).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"touch nope"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Denied.");
        _ = resolver.Requests.ShouldHaveSingleItem();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenSuccessful_UsesExplicitShellAndExactResolvedSecurityEvidence()
    {
        var resolver = new RecordingProcessResolver();
        var runner = new RecordingProcessRunner
        {
            Result = new ProcessRunResult(
                ProcessRunStatus.Exited,
                0,
                [.. "out"u8],
                [.. "warn"u8],
                3,
                4,
                false,
                false,
                ProcessSideEffectCertainty.Completed,
                null),
        };
        var authority = new RecordingSecurityAuthority();

        var result = await CreateTool(resolver, runner, authority).InvokeAsync(
            Request(
                /*lang=json,strict*/
                """{"command":"printf '%s' hi","working_directory":"src","workspace_access":"read_only","timeout_ms":500,"maximum_output_bytes":12}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var unresolved = resolver.Requests.ShouldHaveSingleItem();
        unresolved.Executable.ShouldBe("/configured/shell");
        unresolved.Arguments.ShouldBe(["--fixed", "printf '%s' hi"]);
        unresolved.WorkingDirectory.ShouldBe(new FileSystemPath("src"));
        unresolved.WorkspaceAccess.ShouldBe(ProcessWorkspaceAccess.ReadOnly);
        unresolved.SideEffectClass.ShouldBe(ProcessSideEffectClass.ReadOnly);
        unresolved.Limits.Timeout.ShouldBe(TimeSpan.FromMilliseconds(500));
        unresolved.Limits.MaximumOutputBytes.ShouldBe(12);

        var host = runner.Requests.ShouldHaveSingleItem();
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Audience.ShouldBe(runner.SecurityAudience);
        security.Kind.ShouldBe(SecurityOperationKind.Process);
        security.Effect.ShouldBe(SecurityEffect.Execute);
        security.Resources.ShouldBe(ProcessSecurityBinding.Resources(host.Intent));
        security.InputFingerprint.ShouldBe(ProcessSecurityBinding.Fingerprint(host.Intent));
        host.Grant.RequestId.ShouldBe(security.Id);

        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("stdout").GetString().ShouldBe("out");
        json.RootElement.GetProperty("stderr").GetString().ShouldBe("warn");
        json.RootElement.GetProperty("effect_certainty").GetString().ShouldBe("Completed");
    }

    [Fact]
    public async Task InvokeAsync_WhenExitCodeNonzero_ReturnsFailedOutcomeWithTypedOutput()
    {
        var runner = new RecordingProcessRunner
        {
            Result = new ProcessRunResult(
                ProcessRunStatus.Exited,
                7,
                [],
                [.. "bad"u8],
                0,
                3,
                false,
                false,
                ProcessSideEffectCertainty.Completed,
                null),
        };

        var result = await CreateTool(
            new RecordingProcessResolver(), runner, new RecordingSecurityAuthority()).InvokeAsync(
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
        var runner = new RecordingProcessRunner
        {
            Result = new ProcessRunResult(
                ProcessRunStatus.Exited,
                0,
                [0xff, 0x00],
                [],
                2,
                0,
                false,
                false,
                ProcessSideEffectCertainty.Completed,
                null),
        };

        var result = await CreateTool(
            new RecordingProcessResolver(), runner, new RecordingSecurityAuthority()).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"binary"}"""),
            TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("stdout").ValueKind.ShouldBe(JsonValueKind.Null);
        json.RootElement.GetProperty("stdout_base64").GetString().ShouldBe("/wA=");
        json.RootElement.GetProperty("stdout_valid_utf8").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenOutputSpilled_ProjectsPortableArtifactIdentity()
    {
        var reference = ArtifactReference();
        var runner = new RecordingProcessRunner
        {
            Result = new ProcessRunResult(
                ProcessRunStatus.Exited,
                0,
                [.. "tail"u8],
                [],
                100,
                0,
                true,
                false,
                ProcessSideEffectCertainty.Completed,
                null,
                reference),
        };

        var result = await CreateTool(
            new RecordingProcessResolver(), runner, new RecordingSecurityAuthority()).InvokeAsync(
            Request(/*lang=json,strict*/ """{"command":"large"}"""),
            TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("stdout_artifact_id").GetString().ShouldBe(reference.Id.ToString());
        json.RootElement.GetProperty("stdout_artifact_version").GetString().ShouldBe("1");
        json.RootElement.GetProperty("stdout_artifact_hash").GetString().ShouldBe("hash");
    }

    private static ArtifactReference ArtifactReference() => new(
        new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new ArtifactVersion("1"),
        new ArtifactDirectoryId("process-output"),
        new ArtifactProfileKey("test"),
        new ArtifactProfileVersion(1),
        new TenantId("tenant"),
        new ArtifactOwnerId("session:owner"),
        new PrincipalId("principal"),
        "application/octet-stream",
        100,
        new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch),
        ArtifactDataClassification.Internal,
        ArtifactOwnershipKind.Session,
        ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false),
        DateTimeOffset.UnixEpoch);

    private static CommandTool CreateTool(
        IProcessIntentResolver resolver,
        IProcessRunner runner,
        ISecurityAuthority authority) => new(
            resolver,
            runner,
            authority,
            new FixedSecurityRequestIdGenerator(),
            new FixedProcessOperationIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(OptionsForTool()));

    private static CommandToolOptions OptionsForTool()
    {
        var options = new CommandToolOptions
        {
            ShellExecutable = "/configured/shell",
            DefaultTimeout = TimeSpan.FromSeconds(30),
            MaximumTimeout = TimeSpan.FromMinutes(10),
        };
        options.ShellArguments.Clear();
        options.ShellArguments.Add("--fixed");
        return options;
    }

    private static ToolInvocationRequest Request(string json) => new(
        new ToolExecutionContext(
            new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new SessionId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                new RunId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                null),
            new ExecutionIdentity(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human,
                ExtensionData.Empty)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);
}

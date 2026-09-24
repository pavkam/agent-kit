// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command;

using AgentKit.Tools;

/// <summary>Runs one explicitly declared shell command through exact authorization and a required sandbox.</summary>
public sealed class CommandTool: IToolInvoker
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private static readonly FileRootId _workspaceRoot = new("workspace");
    private static readonly JsonElement _inputSchema = JsonDocument.Parse(
        """
        {
          "type": "object",
          "properties": {
            "command": { "type": "string" },
            "working_directory": { "type": ["string", "null"] },
            "workspace_access": { "type": "string", "enum": ["read_only", "read_write"], "default": "read_write" },
            "timeout_ms": { "type": "integer", "minimum": 1 },
            "maximum_output_bytes": { "type": "integer", "minimum": 1 }
          },
          "required": ["command"],
          "additionalProperties": false
        }
        """).RootElement;

    private readonly IProcessExecutorSelector _executorSelector;
    private readonly ProcessExecutorKey _executorKey;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityRequestIds;
    private readonly IIdentifierGenerator<ProcessOperationId> _processOperationIds;
    private readonly TimeProvider _timeProvider;
    private readonly string _shellExecutable;
    private readonly ImmutableArray<string> _shellArguments;
    private readonly ImmutableArray<ProcessEnvironmentVariable> _environment;
    private readonly SandboxProfileId _sandboxProfile;
    private readonly TimeSpan _defaultTimeout;
    private readonly TimeSpan _maximumTimeout;
    private readonly long _defaultMaximumOutputBytes;
    private readonly long _maximumOutputBytes;
    private readonly TimeSpan _terminationGracePeriod;
    private readonly long _maximumCommandBytes;

    /// <summary>The stable tool identity.</summary>
    public static readonly ToolId Id = new("command");

    /// <summary>Initializes the explicit shell tool over provider-neutral process and security contracts.</summary>
    /// <param name="executorSelector">The selector that resolves the keyed process executor profile.</param>
    /// <param name="authoritySelector">The security authority selector for the resolved process effect.</param>
    /// <param name="securityRequestIds">The replaceable security-request identity source.</param>
    /// <param name="processOperationIds">The replaceable process-operation identity source.</param>
    /// <param name="timeProvider">The deterministic security-deadline clock.</param>
    /// <param name="options">The captured shell identity and model-facing bounds.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">The shell configuration is malformed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
    public CommandTool(
        IProcessExecutorSelector executorSelector,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IIdentifierGenerator<ProcessOperationId> processOperationIds,
        TimeProvider timeProvider,
        IOptions<CommandToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(executorSelector);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(processOperationIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options.Value);
        _executorSelector = executorSelector;
        _executorKey = options.Value.ProcessExecutorKey;
        _authoritySelector = authoritySelector;
        _securityRequestIds = securityRequestIds;
        _processOperationIds = processOperationIds;
        _timeProvider = timeProvider;
        _shellExecutable = options.Value.ShellExecutable;
        _shellArguments = [.. options.Value.ShellArguments];
        _environment = [.. options.Value.EnvironmentVariables
            .OrderBy(static item => item.Key, StringComparer.Ordinal)
            .Select(static item => new ProcessEnvironmentVariable(item.Key, item.Value))];
        _sandboxProfile = options.Value.SandboxProfile;
        _defaultTimeout = options.Value.DefaultTimeout;
        _maximumTimeout = options.Value.MaximumTimeout;
        _defaultMaximumOutputBytes = options.Value.DefaultMaximumOutputBytes;
        _maximumOutputBytes = options.Value.MaximumOutputBytes;
        _terminationGracePeriod = options.Value.TerminationGracePeriod;
        _maximumCommandBytes = options.Value.MaximumCommandBytes;
    }

    /// <summary>Gets the immutable descriptor shared with registration and presentation formatting.</summary>
    public static ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "command",
        "Runs one command through an explicitly configured shell in a required no-network workspace sandbox. Output retains separate bounded stdout and stderr tails.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), _inputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.Mutating, idempotency: null, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.Unspecified, concurrencyKey: null, expectedDuration: null, approvalMayBeCached: null),
        new ToolSourceId("agentkit.tools.command"),
        ExtensionData.Empty);

    internal static ToolDescriptor PresentationDescriptor => Descriptor;

    /// <summary>Gets the default toolset publication selecting this tool from the application tool source.</summary>
    public static ToolsetPublication DefaultToolset { get; } = new(
        new ToolsetKey("agentkit.tools.command"),
        new ToolsetVersion(1),
        new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1)),
        [new ToolsetSourceSelection(ApplicationToolSources.Default)],
        [new ToolAliasAssignment(new ToolAlias("command"), new ToolIdentity(Id, Descriptor.Version))]);

    /// <inheritdoc/>
    public ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationContext context,
        CancellationToken cancellationToken = default) =>
        InvokeCoreAsync(ToExecutionContext(context), context.Arguments, cancellationToken);

    private static ToolExecutionContext ToExecutionContext(ToolInvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var authorization = context.InvocationGrant.Authorization
            ?? throw new InvalidOperationException("Tool invocations require grants that retain complete authorization evidence.");
        return new ToolExecutionContext(
            context.AgentId,
            context.SessionId,
            context.CallId,
            context.InvocationGrant.Scope.Correlation,
            context.InvocationGrant.Identity,
            authorization,
            sessionProfile: null);
    }

    private async ValueTask<ToolInvocationResult> InvokeCoreAsync(
        ToolExecutionContext executionContext,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryParse(arguments, out var parsed, out var error))
        {
            return Failure(error!, "InvalidArguments", [], ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var selection = await _executorSelector.SelectAsync(_executorKey, cancellationToken).ConfigureAwait(false);
        if (selection is ProcessExecutorMissing)
        {
            return Failure(
                "The configured process executor profile is not registered.",
                "Unsupported",
                [],
                ToolTerminalStatus.Unsupported,
                SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (selection is not ProcessExecutorSelected selectedExecutor)
        {
            return Failure(
                "The process executor profile could not be selected.",
                "ProtocolFailed",
                [],
                ToolTerminalStatus.ProtocolFailed,
                SideEffectCertainty.DefinitelyNotPerformed);
        }

        var startRequest = CreateStartRequest(executionContext, parsed);
        var resolution = await selectedExecutor.Resolver.ResolveAsync(startRequest, cancellationToken).ConfigureAwait(false);
        if (resolution is ExecutableResolutionFailed failed)
        {
            return Failure(
                failed.SafeMessage,
                ProcessResolutionStatus.Failed.ToString(),
                [],
                ToolTerminalStatus.InvocationFailed,
                SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (resolution is not ExecutableResolved { Resolved: var resolved })
        {
            return Failure(
                "The command intent could not be resolved safely.",
                ProcessResolutionStatus.Failed.ToString(),
                [],
                ToolTerminalStatus.InvocationFailed,
                SideEffectCertainty.DefinitelyNotPerformed);
        }

        var intent = ProcessStartBinding.ToResolvedProcessIntent(resolved);
        var authorization = executionContext.Authorization;
        var activated = await _authoritySelector.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (activated is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return Failure("The captured security authority is unavailable.", "Denied", [], ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var decision = await selected.Authority.AuthorizeAsync(
            new SecurityRequest(
                _securityRequestIds.Create(),
                authorization.Scope,
                executionContext.ToolCallId,
                authorization.Identity,
                authorization,
                selectedExecutor.Executor.SecurityAudience,
                SecurityOperationKind.Process,
                SecurityEffect.Execute,
                ProcessSecurityBinding.Resources(intent),
                ProcessSecurityBinding.Fingerprint(intent),
                _timeProvider.GetUtcNow().AddMinutes(1)),
            hooks: null,
            cancellationToken).ConfigureAwait(false);
        if (decision is SecurityDenied denied)
        {
            return Failure(denied.Denial.SafeMessage, "Denied", [], ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (decision is not SecurityAllowed allowed)
        {
            return Failure("The security authority returned an unsupported decision.", "Denied", [], ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var start = await selectedExecutor.Executor.StartAsync(resolved, allowed.Grant, cancellationToken).ConfigureAwait(false);
        if (start is ProcessStartDenied deniedStart)
        {
            return Failure(
                deniedStart.SafeMessage,
                "Denied",
                [],
                ToolTerminalStatus.Denied,
                SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (start is ProcessStartResolutionFailed resolutionFailed)
        {
            return Failure(
                resolutionFailed.SafeMessage,
                ProcessResolutionStatus.Failed.ToString(),
                [],
                ToolTerminalStatus.InvocationFailed,
                SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (start is ProcessStartSandboxUnavailable sandboxUnavailable)
        {
            return Failure(
                sandboxUnavailable.SafeMessage,
                ProcessRunStatus.SandboxUnavailable.ToString(),
                [],
                ToolTerminalStatus.Unsupported,
                SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (start is ProcessStartCancelled cancelledStart)
        {
            return Failure(
                "The command was cancelled before it started.",
                ProcessRunStatus.Cancelled.ToString(),
                [],
                ToolTerminalStatus.Cancelled,
                cancelledStart.SideEffectCertainty);
        }

        if (start is ProcessStartFailed failedStart)
        {
            return Failure(
                failedStart.SafeMessage,
                ProcessRunStatus.Failed.ToString(),
                [],
                ToolTerminalStatus.InvocationFailed,
                SideEffectCertainty.DefinitelyNotPerformed);
        }

        if (start is not ProcessHandleStarted started)
        {
            return Failure(
                "The process host returned an unsupported start outcome.",
                ProcessRunStatus.Failed.ToString(),
                [],
                ToolTerminalStatus.ProtocolFailed,
                SideEffectCertainty.DefinitelyNotPerformed);
        }

        await using var handle = started.Handle;
        var settlement = await ReadSettlementAsync(handle, parsed.MaximumOutputBytes, cancellationToken).ConfigureAwait(false);
        var content = Project(settlement);
        return settlement.IsSuccess
            ? new ToolInvocationResult(
                new ToolCallOutcome(
                    ToolCallOutcomeKind.Success,
                    ToolTerminalStatus.Succeeded,
                    settlement.SideEffectCertainty,
                    false,
                    null,
                    Status(settlement.StatusLabel)), content)
            : Failure(
                settlement.Message ?? ExitFailure(settlement),
                settlement.StatusLabel,
                content,
                settlement.ToolStatus,
                settlement.SideEffectCertainty);
    }

    private ProcessStartRequest CreateStartRequest(ToolExecutionContext context, ParsedArguments parsed)
    {
        var shellArguments = _shellArguments.Add(parsed.Command);
        var relativeWorkingDirectory = parsed.WorkingDirectory is null
            ? new NormalizedRelativePath(".")
            : new NormalizedRelativePath(parsed.WorkingDirectory.Value.Value);
        RunId? runId = context.Correlation is InRunOperationCorrelation inRun ? inRun.RunId : null;
        return new ProcessStartRequest(
            _processOperationIds.Create(),
            context.Correlation.OperationId,
            context.AgentId,
            runId,
            new ProcessExecutableReference(_shellExecutable),
            [.. shellArguments.Select(static argument => new ProcessArgument(argument))],
            new FileTarget(_workspaceRoot, relativeWorkingDirectory),
            new EnvironmentProjection(_environment),
            null,
            _sandboxProfile,
            new ProcessResourceLimits(parsed.Timeout, parsed.MaximumOutputBytes, _terminationGracePeriod),
            parsed.WorkspaceAccess == ProcessWorkspaceAccess.ReadOnly
                ? ProcessEffectClass.ReadOnlyObservation
                : ProcessEffectClass.WorkspaceMutation);
    }

    private static async ValueTask<CommandSettlement> ReadSettlementAsync(
        IProcessHandle handle,
        long maximumOutputBytes,
        CancellationToken cancellationToken)
    {
        var stdoutTail = new List<byte>();
        var stderrTail = new List<byte>();
        long totalStdout = 0;
        long totalStderr = 0;
        var stdoutTruncated = false;
        var stderrTruncated = false;
        var outputLimit = checked((int) maximumOutputBytes);
        await foreach (var evt in handle.ReadOutputAsync(cancellationToken).ConfigureAwait(false))
        {
            switch (evt)
            {
                case ProcessStandardOutputBytes stdout:
                    totalStdout += stdout.Bytes.Length;
                    AppendBounded(stdoutTail, stdout.Bytes.Span, outputLimit, ref stdoutTruncated);
                    break;
                case ProcessStandardErrorBytes stderr:
                    totalStderr += stderr.Bytes.Length;
                    AppendBounded(stderrTail, stderr.Bytes.Span, outputLimit, ref stderrTruncated);
                    break;
                case ProcessStandardOutputTruncated truncated:
                    stdoutTruncated = true;
                    totalStdout = Math.Max(totalStdout, truncated.BytesObserved);
                    break;
                case ProcessStandardErrorTruncated truncated:
                    stderrTruncated = true;
                    totalStderr = Math.Max(totalStderr, truncated.BytesObserved);
                    break;
                default:
                    break;
            }
        }

        var exit = await handle.Completion.ConfigureAwait(false);
        return CommandSettlement.FromExit(
            exit,
            [.. stdoutTail],
            [.. stderrTail],
            totalStdout,
            totalStderr,
            stdoutTruncated,
            stderrTruncated);
    }

    private static void AppendBounded(List<byte> tail, ReadOnlySpan<byte> chunk, int limit, ref bool truncated)
    {
        if (limit <= 0)
        {
            truncated = chunk.Length > 0 || truncated;
            return;
        }

        foreach (var value in chunk)
        {
            if (tail.Count < limit)
            {
                tail.Add(value);
            }
            else
            {
                truncated = true;
            }
        }
    }

    private bool TryParse(JsonElement arguments, out ParsedArguments parsed, out string? error)
    {
        parsed = default;
        error = null;
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("command", out var rawCommand)
            || rawCommand.ValueKind != JsonValueKind.String)
        {
            error = "A string 'command' is required.";
            return false;
        }

        var command = rawCommand.GetString()!;
        try
        {
            if (command.Contains('\0', StringComparison.Ordinal)
                || _strictUtf8.GetByteCount(command) > _maximumCommandBytes)
            {
                error = "The command contains NUL or exceeds the configured byte boundary.";
                return false;
            }
        }
        catch (EncoderFallbackException)
        {
            error = "The command contains invalid Unicode scalar data.";
            return false;
        }

        try
        {
            var workingDirectory = OptionalPath(arguments, "working_directory");
            if (!TryWorkspaceAccess(arguments, out var workspaceAccess)
                || !TryPositiveMilliseconds(arguments, "timeout_ms", _defaultTimeout, _maximumTimeout, out var timeout)
                || !TryPositiveBound(
                    arguments,
                    "maximum_output_bytes",
                    _defaultMaximumOutputBytes,
                    _maximumOutputBytes,
                    out var maximumOutputBytes))
            {
                error = "Command options must be within the configured host ceilings.";
                return false;
            }

            parsed = new ParsedArguments(command, workingDirectory, workspaceAccess, timeout, maximumOutputBytes);
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static ImmutableArray<ContentPart> Project(CommandSettlement settlement)
    {
        var stdout = Decode(settlement.StandardOutputTail);
        var stderr = Decode(settlement.StandardErrorTail);
        var json = JsonSerializer.Serialize(new
        {
            status = settlement.StatusLabel,
            exit_code = settlement.ExitCode,
            stdout = stdout.Text,
            stdout_base64 = stdout.Base64,
            stdout_valid_utf8 = stdout.ValidUtf8,
            stderr = stderr.Text,
            stderr_base64 = stderr.Base64,
            stderr_valid_utf8 = stderr.ValidUtf8,
            total_stdout_bytes = settlement.TotalStandardOutputBytes,
            total_stderr_bytes = settlement.TotalStandardErrorBytes,
            stdout_truncated = settlement.StandardOutputTruncated,
            stderr_truncated = settlement.StandardErrorTruncated,
            stdout_artifact_id = (string?) null,
            stdout_artifact_version = (string?) null,
            stdout_artifact_hash = (string?) null,
            stderr_artifact_id = (string?) null,
            stderr_artifact_version = (string?) null,
            stderr_artifact_hash = (string?) null,
            effect_certainty = settlement.SideEffectCertainty.ToString(),
            message = settlement.Message,
        });
        return [new TextPart(json, TextSemantics.Code, ExtensionData.Empty)];
    }

    private static DecodedOutput Decode(ImmutableArray<byte> bytes)
    {
        try
        {
            return new DecodedOutput(_strictUtf8.GetString(bytes.AsSpan()), null, true);
        }
        catch (DecoderFallbackException)
        {
            return new DecodedOutput(null, Convert.ToBase64String(bytes.AsSpan()), false);
        }
    }

    private static FileSystemPath? OptionalPath(JsonElement arguments, string name) =>
        !arguments.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null
            ? null
            : property.ValueKind == JsonValueKind.String
                ? new FileSystemPath(property.GetString()!)
                : throw new ArgumentException($"Property '{name}' must be a string or null.", name);

    private static bool TryWorkspaceAccess(JsonElement arguments, out ProcessWorkspaceAccess access)
    {
        if (!arguments.TryGetProperty("workspace_access", out var property))
        {
            access = ProcessWorkspaceAccess.ReadWrite;
            return true;
        }

        if (property.ValueKind == JsonValueKind.String && property.GetString() == "read_only")
        {
            access = ProcessWorkspaceAccess.ReadOnly;
            return true;
        }

        access = ProcessWorkspaceAccess.ReadWrite;
        return property.ValueKind == JsonValueKind.String && property.GetString() == "read_write";
    }

    private static bool TryPositiveMilliseconds(
        JsonElement arguments,
        string name,
        TimeSpan fallback,
        TimeSpan ceiling,
        out TimeSpan value)
    {
        if (!arguments.TryGetProperty(name, out var property))
        {
            value = fallback;
            return true;
        }

        if (property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out var milliseconds)
            && milliseconds > 0
            && milliseconds <= ceiling.TotalMilliseconds)
        {
            value = TimeSpan.FromMilliseconds(milliseconds);
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryPositiveBound(
        JsonElement arguments,
        string name,
        long fallback,
        long ceiling,
        out long value)
    {
        if (!arguments.TryGetProperty(name, out var property))
        {
            value = fallback;
            return true;
        }

        value = 0;
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out value) && value > 0 && value <= ceiling;
    }

    private static void ValidateOptions(CommandToolOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.ShellExecutable, nameof(value.ShellExecutable));
        ArgumentException.ThrowIfContainsNul(value.ShellExecutable, nameof(value.ShellExecutable));
        ArgumentException.ThrowIfContainsNull([.. value.ShellArguments], nameof(value.ShellArguments));
        foreach (var argument in value.ShellArguments)
        {
            ArgumentException.ThrowIfContainsNul(argument, nameof(value.ShellArguments));
        }
        foreach (var (name, environmentValue) in value.EnvironmentVariables)
        {
            _ = new ProcessEnvironmentVariable(name, environmentValue);
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(value.SandboxProfile.Value, nameof(value.SandboxProfile));
        ArgumentOutOfRangeException.ThrowIfEqual(value.ProcessExecutorKey, default, nameof(value.ProcessExecutorKey));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value.DefaultTimeout, TimeSpan.Zero, nameof(value.DefaultTimeout));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value.MaximumTimeout, TimeSpan.Zero, nameof(value.MaximumTimeout));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value.DefaultTimeout, value.MaximumTimeout, nameof(value.DefaultTimeout));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.DefaultMaximumOutputBytes, nameof(value.DefaultMaximumOutputBytes));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumOutputBytes, nameof(value.MaximumOutputBytes));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            value.DefaultMaximumOutputBytes, value.MaximumOutputBytes, nameof(value.DefaultMaximumOutputBytes));
        ArgumentOutOfRangeException.ThrowIfLessThan(value.TerminationGracePeriod, TimeSpan.Zero, nameof(value.TerminationGracePeriod));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value.MaximumCommandBytes, nameof(value.MaximumCommandBytes));
    }

    private static string ExitFailure(CommandSettlement settlement) => settlement.ExitCode is int code
        ? $"The command exited with code {code}."
        : "The command did not settle successfully.";

    private static ExtensionData Status(string status) => new(
        ImmutableDictionary<string, ExtensionValue>.Empty.Add(
            "agentkit.command.status",
            new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])));

    private static ToolInvocationResult Failure(
        string reason,
        string status,
        ImmutableArray<ContentPart> content,
        ToolTerminalStatus sourceStatus,
        SideEffectCertainty certainty) => new(
            new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, Status(status)), content);

    private readonly record struct ParsedArguments(
        string Command,
        FileSystemPath? WorkingDirectory,
        ProcessWorkspaceAccess WorkspaceAccess,
        TimeSpan Timeout,
        long MaximumOutputBytes);

    private readonly record struct DecodedOutput(string? Text, string? Base64, bool ValidUtf8);

    private readonly record struct CommandSettlement(
        string StatusLabel,
        int? ExitCode,
        ImmutableArray<byte> StandardOutputTail,
        ImmutableArray<byte> StandardErrorTail,
        long TotalStandardOutputBytes,
        long TotalStandardErrorBytes,
        bool StandardOutputTruncated,
        bool StandardErrorTruncated,
        SideEffectCertainty SideEffectCertainty,
        string? Message,
        ToolTerminalStatus ToolStatus,
        bool IsSuccess)
    {
        internal static CommandSettlement FromExit(
            ProcessExitResult exit,
            ImmutableArray<byte> stdoutTail,
            ImmutableArray<byte> stderrTail,
            long totalStdout,
            long totalStderr,
            bool stdoutTruncated,
            bool stderrTruncated) => exit switch
            {
                ProcessExited exited => new CommandSettlement(
                    ProcessRunStatus.Exited.ToString(),
                    exited.ExitCode,
                    stdoutTail,
                    stderrTail,
                    totalStdout,
                    totalStderr,
                    stdoutTruncated,
                    stderrTruncated,
                    exited.SideEffectCertainty,
                    null,
                    exited.ExitCode == 0 ? ToolTerminalStatus.Succeeded : ToolTerminalStatus.InvocationFailed,
                    exited.ExitCode == 0),
                ProcessCancelled cancelled => new CommandSettlement(
                    ProcessRunStatus.Cancelled.ToString(),
                    null,
                    stdoutTail,
                    stderrTail,
                    totalStdout,
                    totalStderr,
                    stdoutTruncated,
                    stderrTruncated,
                    cancelled.SideEffectCertainty,
                    "The command was cancelled.",
                    ToolTerminalStatus.Cancelled,
                    false),
                ProcessTimedOut timedOut => new CommandSettlement(
                    ProcessRunStatus.TimedOut.ToString(),
                    null,
                    stdoutTail,
                    stderrTail,
                    totalStdout,
                    totalStderr,
                    stdoutTruncated,
                    stderrTruncated,
                    timedOut.SideEffectCertainty,
                    "The command timed out.",
                    ToolTerminalStatus.TimedOut,
                    false),
                ProcessSignalled signalled => new CommandSettlement(
                    ProcessRunStatus.Failed.ToString(),
                    null,
                    stdoutTail,
                    stderrTail,
                    totalStdout,
                    totalStderr,
                    stdoutTruncated,
                    stderrTruncated,
                    signalled.SideEffectCertainty,
                    $"The command terminated with signal {signalled.Signal}.",
                    ToolTerminalStatus.InvocationFailed,
                    false),
                ProcessKilled killed => new CommandSettlement(
                    ProcessRunStatus.Failed.ToString(),
                    null,
                    stdoutTail,
                    stderrTail,
                    totalStdout,
                    totalStderr,
                    stdoutTruncated,
                    stderrTruncated,
                    killed.SideEffectCertainty,
                    "The command was forcibly killed.",
                    ToolTerminalStatus.InvocationFailed,
                    false),
                _ => new CommandSettlement(
                    ProcessRunStatus.Failed.ToString(),
                    null,
                    stdoutTail,
                    stderrTail,
                    totalStdout,
                    totalStderr,
                    stdoutTruncated,
                    stderrTruncated,
                    exit.SideEffectCertainty,
                    "The command did not settle successfully.",
                    ToolTerminalStatus.InvocationFailed,
                    false),
            };
    }
}

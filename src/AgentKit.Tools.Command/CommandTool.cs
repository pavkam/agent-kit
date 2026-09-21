// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Command;

/// <summary>Runs one explicitly declared shell command through exact authorization and a required sandbox.</summary>
public sealed class CommandTool: ITool
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
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

    private readonly IProcessIntentResolver _resolver;
    private readonly IProcessRunner _runner;
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
    /// <param name="resolver">The resolver that canonicalizes the shell, working directory, and bounds before authorization.</param>
    /// <param name="runner">The effecting process boundary that revalidates intent and consumes the exact grant.</param>
    /// <param name="authoritySelector">The security authority selector for the resolved process effect.</param>
    /// <param name="securityRequestIds">The replaceable security-request identity source.</param>
    /// <param name="processOperationIds">The replaceable process-operation identity source.</param>
    /// <param name="timeProvider">The deterministic security-deadline clock.</param>
    /// <param name="options">The captured shell identity and model-facing bounds.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">The shell configuration is malformed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
    public CommandTool(
        IProcessIntentResolver resolver,
        IProcessRunner runner,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> securityRequestIds,
        IIdentifierGenerator<ProcessOperationId> processOperationIds,
        TimeProvider timeProvider,
        IOptions<CommandToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(securityRequestIds);
        ArgumentNullException.ThrowIfNull(processOperationIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options.Value);
        _resolver = resolver;
        _runner = runner;
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

    /// <summary>Gets the immutable descriptor shared with exact presentation formatting.</summary>
    /// <value>The source-owned identity, schema, effects, and hints for this tool.</value>
    internal static ToolDescriptor PresentationDescriptor { get; } = new(
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

    /// <inheritdoc/>
    public ToolDescriptor Descriptor => PresentationDescriptor;

    /// <inheritdoc/>
    public async Task<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryParse(request.Arguments, out var parsed, out var error))
        {
            return Failure(error!, "InvalidArguments", [], ToolTerminalStatus.InvalidArguments, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var arguments = _shellArguments.Add(parsed.Command);
        var unresolved = new ProcessResolveRequest(
            _processOperationIds.Create(),
            _shellExecutable,
            arguments,
            parsed.WorkingDirectory,
            _environment,
            [],
            _sandboxProfile,
            parsed.WorkspaceAccess,
            parsed.WorkspaceAccess == ProcessWorkspaceAccess.ReadOnly
                ? ProcessSideEffectClass.ReadOnly
                : ProcessSideEffectClass.WorkspaceMutation,
            ProcessChildPolicy.AllowSandboxed,
            new ProcessResourceLimits(parsed.Timeout, parsed.MaximumOutputBytes, _terminationGracePeriod));
        var resolution = await _resolver.ResolveAsync(unresolved, cancellationToken).ConfigureAwait(false);
        if (resolution.Status != ProcessResolutionStatus.Resolved || resolution.Intent is null)
        {
            return Failure(
                resolution.SafeMessage ?? "The command intent could not be resolved safely.",
                resolution.Status.ToString(),
                [], resolution.Status switch
                {
                    ProcessResolutionStatus.InvalidIntent => ToolTerminalStatus.InvalidArguments,
                    ProcessResolutionStatus.ExecutableRejected or ProcessResolutionStatus.WorkingDirectoryRejected => ToolTerminalStatus.Unsupported,
                    ProcessResolutionStatus.Failed => ToolTerminalStatus.InvocationFailed,
                    ProcessResolutionStatus.Resolved => ToolTerminalStatus.ProtocolFailed,
                    _ => ToolTerminalStatus.ProtocolFailed,
                }, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var intent = resolution.Intent;
        var context = request.Context;
        var authorization = context.Authorization;
        var activated = await _authoritySelector.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (activated is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return Failure("The captured security authority is unavailable.", "Denied", [], ToolTerminalStatus.Denied, SideEffectCertainty.DefinitelyNotPerformed);
        }

        var decision = await selected.Authority.AuthorizeAsync(
            new SecurityRequest(
                _securityRequestIds.Create(),
                authorization.Scope,
                context.ToolCallId,
                authorization.Identity,
                authorization,
                _runner.SecurityAudience,
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

        var result = await _runner.RunAsync(new ProcessRunRequest(intent, allowed.Grant), cancellationToken)
            .ConfigureAwait(false);
        var content = Project(result);
        return result.Status == ProcessRunStatus.Exited && result.ExitCode == 0
            ? new ToolInvocationResult(
                new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, EffectCertainty(result.EffectCertainty), false, null, Status(result.Status.ToString())), content)
            : Failure(
                result.SafeMessage ?? ExitFailure(result),
                result.Status.ToString(),
                content, result.Status switch
                {
                    ProcessRunStatus.Denied => ToolTerminalStatus.Denied,
                    ProcessRunStatus.SandboxUnavailable => ToolTerminalStatus.Unsupported,
                    ProcessRunStatus.TimedOut => ToolTerminalStatus.TimedOut,
                    ProcessRunStatus.Cancelled => ToolTerminalStatus.Cancelled,
                    ProcessRunStatus.Exited or ProcessRunStatus.ResolutionFailed or ProcessRunStatus.LimitExceeded or ProcessRunStatus.Failed => ToolTerminalStatus.InvocationFailed,
                    _ => ToolTerminalStatus.InvocationFailed,
                }, EffectCertainty(result.EffectCertainty));
    }

    private static SideEffectCertainty EffectCertainty(ProcessSideEffectCertainty certainty) => certainty switch
    {
        ProcessSideEffectCertainty.NotStarted => SideEffectCertainty.DefinitelyNotPerformed,
        ProcessSideEffectCertainty.Completed => SideEffectCertainty.DefinitelyPerformed,
        ProcessSideEffectCertainty.MayHaveOccurred => SideEffectCertainty.Unknown,
        _ => SideEffectCertainty.Unknown,
    };

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

    private static ImmutableArray<ContentPart> Project(ProcessRunResult result)
    {
        var stdout = Decode(result.StandardOutputTail);
        var stderr = Decode(result.StandardErrorTail);
        var json = JsonSerializer.Serialize(new
        {
            status = result.Status.ToString(),
            exit_code = result.ExitCode,
            stdout = stdout.Text,
            stdout_base64 = stdout.Base64,
            stdout_valid_utf8 = stdout.ValidUtf8,
            stderr = stderr.Text,
            stderr_base64 = stderr.Base64,
            stderr_valid_utf8 = stderr.ValidUtf8,
            total_stdout_bytes = result.TotalStandardOutputBytes,
            total_stderr_bytes = result.TotalStandardErrorBytes,
            stdout_truncated = result.StandardOutputTruncated,
            stderr_truncated = result.StandardErrorTruncated,
            stdout_artifact_id = result.StandardOutputArtifact?.Id.ToString(),
            stdout_artifact_version = result.StandardOutputArtifact?.Version.Value,
            stdout_artifact_hash = result.StandardOutputArtifact?.Integrity.ContentHash.Value,
            stderr_artifact_id = result.StandardErrorArtifact?.Id.ToString(),
            stderr_artifact_version = result.StandardErrorArtifact?.Version.Value,
            stderr_artifact_hash = result.StandardErrorArtifact?.Integrity.ContentHash.Value,
            effect_certainty = result.EffectCertainty.ToString(),
            message = result.SafeMessage,
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

    private static string ExitFailure(ProcessRunResult result) => result.Status == ProcessRunStatus.Exited
        ? $"The command exited with code {result.ExitCode}."
        : "The command did not settle successfully.";

    private static ExtensionData Status(string status) => new(
        ImmutableDictionary<string, ExtensionValue>.Empty.Add(
            "agentkit.command.status",
            new ExtensionValue([.. JsonSerializer.SerializeToUtf8Bytes(status)])));

    private static ToolInvocationResult Failure(
        string reason,
        string status,
        ImmutableArray<ContentPart> content, ToolTerminalStatus sourceStatus, SideEffectCertainty certainty) => new(
            new ToolCallOutcome(sourceStatus.ToOutcomeKind(), sourceStatus, certainty, false, reason, Status(status)), content);

    private readonly record struct ParsedArguments(
        string Command,
        FileSystemPath? WorkingDirectory,
        ProcessWorkspaceAccess WorkspaceAccess,
        TimeSpan Timeout,
        long MaximumOutputBytes);

    private readonly record struct DecodedOutput(string? Text, string? Base64, bool ValidUtf8);
}

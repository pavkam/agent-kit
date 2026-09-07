// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Resolves process intents from captured deterministic mappings without observing the host.</summary>
public sealed partial class ScriptedProcessIntentResolver: IProcessIntentResolver
{
    private readonly string _workspaceRoot;
    private readonly ImmutableDictionary<string, ScriptedExecutable> _executables;
    private readonly ImmutableHashSet<string> _environmentNames;
    private readonly ILogger<ScriptedProcessIntentResolver> _logger;

    /// <summary>Initializes a host-independent resolver from one captured options snapshot.</summary>
    /// <param name="options">The synthetic workspace and exact executable mappings.</param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">The workspace or a mapping is invalid or duplicated.</exception>
    public ScriptedProcessIntentResolver(
        IOptions<ScriptedProcessOptions> options,
        ILogger<ScriptedProcessIntentResolver>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.WorkspaceRoot);
        if (!Path.IsPathRooted(options.Value.WorkspaceRoot))
        {
            throw new ArgumentException("The scripted workspace root must be absolute.", nameof(options));
        }

        _workspaceRoot = Path.GetFullPath(options.Value.WorkspaceRoot);
        try
        {
            _executables = options.Value.Executables.ToImmutableDictionary(static item => item.Reference, StringComparer.Ordinal);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Scripted executable references must be unique.", nameof(options), exception);
        }

        _environmentNames = options.Value.AllowedEnvironmentVariableNames.ToImmutableHashSet(StringComparer.Ordinal);
        if (_environmentNames.Count != options.Value.AllowedEnvironmentVariableNames.Count)
        {
            throw new ArgumentException("Scripted environment names must be unique.", nameof(options));
        }

        _logger = logger ?? NullLogger<ScriptedProcessIntentResolver>.Instance;
    }

    /// <inheritdoc/>
    private ValueTask<ProcessResolutionResult> ResolveCoreAsync(
        ProcessResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_executables.TryGetValue(request.Executable, out var executable))
        {
            return ValueTask.FromResult(Failure(
                ProcessResolutionStatus.ExecutableRejected,
                "The scripted executable reference is not configured."));
        }

        var environmentNames = request.Environment.Select(static item => item.Name).ToArray();
        if (environmentNames.Distinct(StringComparer.Ordinal).Count() != environmentNames.Length
            || environmentNames.Any(name => !_environmentNames.Contains(name)))
        {
            return ValueTask.FromResult(Failure(
                ProcessResolutionStatus.InvalidIntent,
                "The environment projection is duplicated or outside the scripted allowlist."));
        }

        var workingDirectory = request.WorkingDirectory is null
            ? _workspaceRoot
            : Path.GetFullPath(Path.Combine(
                _workspaceRoot,
                request.WorkingDirectory.Value.Value.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(workingDirectory, _workspaceRoot))
        {
            return ValueTask.FromResult(Failure(
                ProcessResolutionStatus.WorkingDirectoryRejected,
                "The working directory is outside the scripted workspace."));
        }

        var environment = request.Environment.OrderBy(static item => item.Name, StringComparer.Ordinal).ToImmutableArray();
        var canonicalRequest = new ProcessResolveRequest(
            request.Id,
            request.Executable,
            request.Arguments,
            request.WorkingDirectory,
            environment,
            request.StandardInput,
            request.SandboxProfile,
            request.WorkspaceAccess,
            request.SideEffectClass,
            request.ChildPolicy,
            request.Limits);
        var environmentFingerprint = ProcessSecurityBinding.FingerprintBytes(JsonSerializer.SerializeToUtf8Bytes(
            environment.Select(static item => new
            {
                item.Name,
                valueFingerprint = ProcessSecurityBinding.FingerprintText(item.Value),
            })));
        var intent = new ResolvedProcessIntent(
            canonicalRequest,
            executable.AbsolutePath,
            executable.Fingerprint,
            _workspaceRoot,
            workingDirectory,
            new ContentHash(environmentFingerprint.Value),
            new ContentHash(ProcessSecurityBinding.FingerprintBytes(request.StandardInput.AsSpan()).Value));
        return ValueTask.FromResult(new ProcessResolutionResult(ProcessResolutionStatus.Resolved, intent, null));
    }

    private static bool IsWithinRoot(string path, string root) =>
        string.Equals(path, root, StringComparison.Ordinal)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static ProcessResolutionResult Failure(ProcessResolutionStatus status, string message) =>
        new(status, null, message);
}

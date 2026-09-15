// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Canonicalizes configured Unix executables and workspace directories before authorization.</summary>
public sealed partial class OperatingSystemProcessIntentResolver: IProcessIntentResolver
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly string _root;
    private readonly HashSet<string> _allowedExecutables;
    private readonly HashSet<string> _allowedEnvironmentNames;
    private readonly ImmutableArray<(string ProfileId, string ConfiguredPath)> _configuredReadOnlyRoots;
    private readonly int _maximumArgumentCount;
    private readonly long _maximumArgumentBytes;
    private readonly long _maximumInputBytes;
    private readonly long _maximumEnvironmentBytes;
    private readonly TimeSpan _maximumTimeout;
    private readonly long _maximumOutputBytes;
    private readonly long _maximumExecutableBytes;
    private readonly ILogger<OperatingSystemProcessIntentResolver> _logger;

    /// <summary>Initializes a resolver from one validated immutable options snapshot.</summary>
    /// <param name="options">The configured workspace, executable allowlist, and input ceilings.</param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">A configured root or executable cannot be canonicalized safely.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
    public OperatingSystemProcessIntentResolver(
        IOptions<OperatingSystemProcessOptions> options,
        ILogger<OperatingSystemProcessIntentResolver>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options.Value);
        _root = CanonicalizeExistingPath(options.Value.RootDirectory)
            ?? throw new ArgumentException("The process workspace root cannot be resolved safely.", nameof(options));
        _allowedExecutables = options.Value.AllowedExecutablePaths
            .Select(CanonicalizeExistingPath)
            .Where(static path => path is not null)
            .Select(static path => path!)
            .ToHashSet(StringComparer.Ordinal);
        if (_allowedExecutables.Count != options.Value.AllowedExecutablePaths.Count)
        {
            throw new ArgumentException("Every allowed executable must resolve safely.", nameof(options));
        }

        _allowedEnvironmentNames = options.Value.AllowedEnvironmentVariableNames.ToHashSet(StringComparer.Ordinal);
        _configuredReadOnlyRoots = [.. options.Value.ReadOnlyToolchainRoots
            .OrderBy(static item => item.Key, StringComparer.Ordinal)
            .Select(static item =>
            {
                var canonicalPath = CanonicalizeExistingPath(item.Value);
                return canonicalPath is not null && Directory.Exists(canonicalPath)
                    ? (item.Key, item.Value)
                    : throw new ArgumentException("Every read-only toolchain root must resolve to an existing directory.");
            })];
        _maximumArgumentCount = options.Value.MaximumArgumentCount;
        _maximumArgumentBytes = options.Value.MaximumArgumentBytes;
        _maximumInputBytes = options.Value.MaximumInputBytes;
        _maximumEnvironmentBytes = options.Value.MaximumEnvironmentBytes;
        _maximumTimeout = options.Value.MaximumTimeout;
        _maximumOutputBytes = options.Value.MaximumOutputBytes;
        _maximumExecutableBytes = options.Value.MaximumExecutableBytes;
        _logger = logger ?? NullLogger<OperatingSystemProcessIntentResolver>.Instance;
    }

    /// <inheritdoc/>
    private async ValueTask<ProcessResolutionResult> ResolveCoreAsync(
        ProcessResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return Failure(ProcessResolutionStatus.Failed, "Secure Unix process resolution is unavailable.");
        }

        if (!ValidateRequestBounds(request, out var boundsFailure))
        {
            return Failure(ProcessResolutionStatus.InvalidIntent, boundsFailure!);
        }

        var executable = CanonicalizeExistingPath(request.Executable);
        if (executable is null || !_allowedExecutables.Contains(executable) || !IsExecutable(executable))
        {
            return Failure(
                ProcessResolutionStatus.ExecutableRejected,
                "The executable is unavailable or outside the configured allowlist.");
        }

        var requestedWorkingDirectory = request.WorkingDirectory is null
            ? _root
            : Path.Combine(_root, request.WorkingDirectory.Value.Value.Replace('/', Path.DirectorySeparatorChar));
        var workingDirectory = CanonicalizeExistingPath(requestedWorkingDirectory);
        if (workingDirectory is null || !Directory.Exists(workingDirectory) || !IsWithinRoot(workingDirectory, _root))
        {
            return Failure(
                ProcessResolutionStatus.WorkingDirectoryRejected,
                "The working directory is unavailable or outside the configured workspace.");
        }

        var executableFingerprint = await HashFileAsync(
            executable, _maximumExecutableBytes, cancellationToken).ConfigureAwait(false);
        if (executableFingerprint is null)
        {
            return Failure(ProcessResolutionStatus.ExecutableRejected, "The executable could not be fingerprinted safely.");
        }

        var readOnlyRoots = ImmutableArray.CreateBuilder<ProcessReadOnlyRoot>(_configuredReadOnlyRoots.Length);
        foreach (var (profileId, configuredPath) in _configuredReadOnlyRoots)
        {
            var canonicalPath = CanonicalizeExistingPath(configuredPath);
            if (canonicalPath is null || !Directory.Exists(canonicalPath))
            {
                return Failure(
                    ProcessResolutionStatus.InvalidIntent,
                    "A captured process read-only root can no longer be resolved safely.");
            }

            readOnlyRoots.Add(new ProcessReadOnlyRoot(profileId, canonicalPath));
        }

        var capturedReadOnlyRoots = readOnlyRoots.MoveToImmutable();
        if (!request.ReadOnlyRoots.IsEmpty && !request.ReadOnlyRoots.SequenceEqual(capturedReadOnlyRoots))
        {
            return Failure(ProcessResolutionStatus.InvalidIntent, "The process read-only roots do not match the captured host profile.");
        }

        var canonicalEnvironment = request.Environment.OrderBy(static item => item.Name, StringComparer.Ordinal).ToImmutableArray();
        var canonicalRequest = new ProcessResolveRequest(
            request.Id,
            executable,
            request.Arguments,
            request.WorkingDirectory,
            canonicalEnvironment,
            request.StandardInput,
            request.SandboxProfile,
            request.WorkspaceAccess,
            request.SideEffectClass,
            request.ChildPolicy,
            request.Limits)
        {
            ReadOnlyRoots = capturedReadOnlyRoots,
        };
        var environmentFingerprint = ProcessSecurityBinding.FingerprintBytes(JsonSerializer.SerializeToUtf8Bytes(
            canonicalEnvironment.Select(static item => new
            {
                item.Name,
                valueFingerprint = ProcessSecurityBinding.FingerprintText(item.Value),
            })));
        var intent = new ResolvedProcessIntent(
            canonicalRequest,
            executable,
            executableFingerprint.Value,
            _root,
            workingDirectory,
            environmentFingerprint,
            ProcessSecurityBinding.FingerprintBytes(request.StandardInput.AsSpan()));
        return new ProcessResolutionResult(ProcessResolutionStatus.Resolved, intent, null);
    }

    private bool ValidateRequestBounds(ProcessResolveRequest request, out string? failure)
    {
        if ((request.SideEffectClass == ProcessSideEffectClass.ReadOnly
                && request.WorkspaceAccess == ProcessWorkspaceAccess.ReadWrite)
            || (request.SideEffectClass == ProcessSideEffectClass.WorkspaceMutation
                && request.WorkspaceAccess != ProcessWorkspaceAccess.ReadWrite))
        {
            failure = "The declared side-effect class is inconsistent with the requested workspace access.";
            return false;
        }

        if (request.Arguments.Length > _maximumArgumentCount)
        {
            failure = "The argument vector exceeds the configured count boundary.";
            return false;
        }

        long argumentBytes = 0;
        try
        {
            foreach (var argument in request.Arguments)
            {
                if (argument.Contains('\0', StringComparison.Ordinal))
                {
                    failure = "Process arguments cannot contain NUL characters.";
                    return false;
                }

                argumentBytes = checked(argumentBytes + _strictUtf8.GetByteCount(argument));
            }
        }
        catch (Exception exception) when (exception is EncoderFallbackException or OverflowException)
        {
            failure = "Process arguments contain invalid or excessive text.";
            return false;
        }

        if (argumentBytes > _maximumArgumentBytes)
        {
            failure = "The argument vector exceeds the configured byte boundary.";
            return false;
        }

        if (request.StandardInput.Length > _maximumInputBytes
            || request.Limits.Timeout <= TimeSpan.Zero
            || request.Limits.Timeout > _maximumTimeout
            || request.Limits.MaximumOutputBytes <= 0
            || request.Limits.MaximumOutputBytes > _maximumOutputBytes
            || request.Limits.TerminationGracePeriod < TimeSpan.Zero)
        {
            failure = "The process input or resource limits exceed the configured host profile.";
            return false;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        long environmentBytes = 0;
        foreach (var variable in request.Environment)
        {
            if (!_allowedEnvironmentNames.Contains(variable.Name)
                || !names.Add(variable.Name)
                || variable.Name.Contains('=', StringComparison.Ordinal)
                || variable.Name.Contains('\0', StringComparison.Ordinal)
                || variable.Value.Contains('\0', StringComparison.Ordinal))
            {
                failure = "The environment projection contains a duplicate, malformed, or disallowed name.";
                return false;
            }

            try
            {
                environmentBytes = checked(
                    environmentBytes
                    + _strictUtf8.GetByteCount(variable.Name)
                    + _strictUtf8.GetByteCount(variable.Value));
            }
            catch (Exception exception) when (exception is EncoderFallbackException or OverflowException)
            {
                failure = "The environment projection contains invalid or excessive text.";
                return false;
            }
        }

        if (environmentBytes > _maximumEnvironmentBytes)
        {
            failure = "The environment projection exceeds the configured byte boundary.";
            return false;
        }

        failure = null;
        return true;
    }

    private static async ValueTask<ContentHash?> HashFileAsync(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total = checked(total + read);
                if (total > maximumBytes)
                {
                    return null;
                }

                hash.AppendData(buffer.AsSpan(0, read));
            }

            return new ContentHash($"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OverflowException)
        {
            return null;
        }
    }

    private static bool IsExecutable(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static bool IsWithinRoot(string path, string root) =>
        string.Equals(path, root, StringComparison.Ordinal)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static string? CanonicalizeExistingPath(string path)
    {
        if ((!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var pointer = RealPath(path, IntPtr.Zero);
        if (pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUTF8(pointer);
        }
        finally
        {
            Free(pointer);
        }
    }

    private static ProcessResolutionResult Failure(ProcessResolutionStatus status, string message) =>
        new(status, null, message);

    private static void ValidateOptions(OperatingSystemProcessOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RootDirectory);
        foreach (var (profileId, path) in options.ReadOnlyToolchainRoots)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(profileId, nameof(options.ReadOnlyToolchainRoots));
            ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(options.ReadOnlyToolchainRoots));
            ArgumentException.ThrowIfPathNotRooted(path, nameof(options.ReadOnlyToolchainRoots));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumArgumentCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumArgumentBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaximumInputBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaximumEnvironmentBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.MaximumTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumOutputBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MaximumOutputBytes, int.MaxValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumConcurrentProcesses);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ForcedTerminationWait, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumExecutableBytes);
    }

    [LibraryImport("libc", EntryPoint = "realpath", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr RealPath(string path, IntPtr resolvedPath);

    [LibraryImport("libc", EntryPoint = "free")]
    private static partial void Free(IntPtr pointer);
}

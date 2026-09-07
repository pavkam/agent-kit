// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Builds no-network workspace-confined launches using macOS sandbox-exec or Linux bubblewrap.</summary>
/// <param name="logger">The optional structured logger; a null value disables log publication.</param>
public sealed partial class PlatformProcessSandboxProvider(ILogger<PlatformProcessSandboxProvider>? logger = null): IProcessSandboxProvider
{
    private readonly ILogger<PlatformProcessSandboxProvider> _logger = logger ?? NullLogger<PlatformProcessSandboxProvider>.Instance;
    private const string _bubblewrapPath = "/usr/bin/bwrap";
    private const string _sandboxExecPath = "/usr/bin/sandbox-exec";

    /// <summary>The stable profile implemented by the platform adapters.</summary>
    public static readonly SandboxProfileId WorkspaceNoNetworkProfile = new("workspace-no-network-v1");

    /// <inheritdoc/>
    public SandboxProfileId ProfileId => WorkspaceNoNetworkProfile;

    /// <inheritdoc/>
    private static ValueTask<ProcessSandboxResult> PrepareCoreAsync(
        ResolvedProcessIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(intent.Request.WorkspaceAccess == ProcessWorkspaceAccess.None
            ? Failure(
                ProcessSandboxStatus.UnsupportedIntent,
                "The platform profile requires an explicit read-only or read-write workspace projection.")
            : OperatingSystem.IsMacOS()
                ? PrepareMacOs(intent)
                : OperatingSystem.IsLinux()
                    ? PrepareLinux(intent)
                    : Failure(
                        ProcessSandboxStatus.Unavailable,
                        "No supported operating-system sandbox is available on this platform."));
    }

    private static ProcessSandboxResult PrepareMacOs(ResolvedProcessIntent intent)
    {
        if (!File.Exists(_sandboxExecPath))
        {
            return Failure(ProcessSandboxStatus.Unavailable, "The macOS sandbox launcher is unavailable.");
        }

        var profile = new StringBuilder("(version 1)\n(deny default)\n")
            .Append(intent.Request.ChildPolicy == ProcessChildPolicy.AllowSandboxed
                ? "(allow process*)\n"
                : $"(allow process-exec (literal \"{EscapeSandboxString(intent.AbsoluteExecutablePath)}\"))\n")
            .Append("(allow signal (target self))\n")
            .Append("(allow sysctl-read)\n")
            .Append("(allow file-read-data (literal \"/\"))\n")
            .Append("(allow file-read* ")
            .Append(PathRule("/System"))
            .Append(' ')
            .Append(PathRule("/Library"))
            .Append(' ')
            .Append(PathRule("/usr"))
            .Append(' ')
            .Append(PathRule("/bin"))
            .Append(' ')
            .Append(PathRule("/sbin"))
            .Append(' ')
            .Append(PathRule("/private/etc"))
            .Append(' ')
            .Append(PathRule("/private/var/select"))
            .Append(' ')
            .Append(PathRule("/dev"))
            .Append(' ')
            .Append(PathRule(intent.AbsoluteWorkspaceRoot))
            .Append(")\n");
        if (intent.Request.WorkspaceAccess == ProcessWorkspaceAccess.ReadWrite)
        {
            _ = profile.Append("(allow file-write* ")
                .Append(PathRule(intent.AbsoluteWorkspaceRoot))
                .Append(")\n");
        }

        _ = profile.Append("(deny network*)\n");
        List<string> arguments = ["-p", profile.ToString(), intent.AbsoluteExecutablePath];
        arguments.AddRange(intent.Request.Arguments);
        return new ProcessSandboxResult(
            ProcessSandboxStatus.Ready,
            new ProcessSandboxLaunch(_sandboxExecPath, [.. arguments]),
            null);
    }

    private static ProcessSandboxResult PrepareLinux(ResolvedProcessIntent intent)
    {
        if (!File.Exists(_bubblewrapPath))
        {
            return Failure(ProcessSandboxStatus.Unavailable, "The Linux bubblewrap launcher is unavailable.");
        }

        if (intent.Request.ChildPolicy == ProcessChildPolicy.Deny)
        {
            return Failure(
                ProcessSandboxStatus.UnsupportedIntent,
                "The Linux sandbox profile cannot prove child-process denial without a configured seccomp policy.");
        }

        List<string> arguments =
        [
            "--die-with-parent",
            "--new-session",
            "--unshare-all",
            "--clearenv",
            "--proc", "/proc",
            "--dev", "/dev",
            "--tmpfs", "/tmp",
        ];
        AddReadOnlyBindIfPresent(arguments, "/usr");
        AddReadOnlyBindIfPresent(arguments, "/bin");
        AddReadOnlyBindIfPresent(arguments, "/sbin");
        AddReadOnlyBindIfPresent(arguments, "/lib");
        AddReadOnlyBindIfPresent(arguments, "/lib64");
        AddReadOnlyBindIfPresent(arguments, "/etc");
        arguments.Add(intent.Request.WorkspaceAccess == ProcessWorkspaceAccess.ReadWrite ? "--bind" : "--ro-bind");
        arguments.Add(intent.AbsoluteWorkspaceRoot);
        arguments.Add(intent.AbsoluteWorkspaceRoot);
        arguments.Add("--chdir");
        arguments.Add(intent.AbsoluteWorkingDirectory);
        foreach (var variable in intent.Request.Environment)
        {
            arguments.Add("--setenv");
            arguments.Add(variable.Name);
            arguments.Add(variable.Value);
        }

        arguments.Add("--");
        arguments.Add(intent.AbsoluteExecutablePath);
        arguments.AddRange(intent.Request.Arguments);
        return new ProcessSandboxResult(
            ProcessSandboxStatus.Ready,
            new ProcessSandboxLaunch(_bubblewrapPath, [.. arguments]),
            null);
    }

    private static void AddReadOnlyBindIfPresent(List<string> arguments, string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        arguments.Add("--ro-bind");
        arguments.Add(path);
        arguments.Add(path);
    }

    private static string PathRule(string path) => $"(subpath \"{EscapeSandboxString(path)}\")";

    private static string EscapeSandboxString(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static ProcessSandboxResult Failure(ProcessSandboxStatus status, string message) =>
        new(status, null, message);
}

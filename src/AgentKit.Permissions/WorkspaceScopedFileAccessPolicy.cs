// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>An illustrative first-party <see cref="ISecurityPolicy"/> that allows file and directory operations over structurally safe relative paths.</summary>
/// <remarks>
/// <para>
/// This is an example, not a complete file-access policy. <see cref="SecurityAuthority"/> denies by default when no
/// policy allows a request, so registering only this policy turns that fail-closed default into "workspace-scoped
/// file and directory access is allowed" for <see cref="SecurityOperationKind.FileRead"/>,
/// <see cref="SecurityOperationKind.DirectoryRead"/>, <see cref="SecurityOperationKind.FileSearch"/>,
/// <see cref="SecurityOperationKind.FileWrite"/>, and <see cref="SecurityOperationKind.DirectoryCreate"/> requests,
/// while granting nothing for any other operation kind.
/// </para>
/// <para>
/// It allows a request only when every resource has kind <see cref="ProtectedResourceKind.File"/> or
/// <see cref="ProtectedResourceKind.Directory"/> and an identifier that is a non-rooted, traversal-free relative
/// path — the same structural invariant <c>FileSystemPath</c> enforces at construction. Re-checking it here is
/// deliberate defense in depth: a policy is evaluated before any effecting boundary and cannot assume every resource
/// it sees was built through that exact validated type. It abstains, rather than denies, when a resource fails that
/// check or is not a file/directory resource, so a more specific policy can still decide; absent one, the
/// authority's fail-closed default still applies.
/// </para>
/// <para>
/// This policy does not know a configured filesystem root and cannot prove a resource actually resolves inside one;
/// that remains the effecting <c>IFileSystem</c>'s own responsibility. It also does not distinguish path prefixes,
/// extensions, size, or tenant scoping. Applications with sharper requirements should replace or compose it with
/// their own <see cref="ISecurityPolicy"/>.
/// </para>
/// </remarks>
public sealed class WorkspaceScopedFileAccessPolicy: ISecurityPolicy
{
    private static readonly SecurityPolicyResult _allowResult = new(
        SecurityPolicyResultKind.Allow,
        "workspace-scoped-file-access",
        "The request targets only structurally safe relative file or directory paths.");

    private static readonly SecurityPolicyResult _abstainResult = new(SecurityPolicyResultKind.Abstain, null, null);

    /// <inheritdoc/>
    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsGovernedOperation(request.Kind))
        {
            return ValueTask.FromResult(_abstainResult);
        }

        foreach (var resource in request.Resources)
        {
            if (!IsSafeFileOrDirectoryResource(resource))
            {
                return ValueTask.FromResult(_abstainResult);
            }
        }

        return ValueTask.FromResult(_allowResult);
    }

    /// <summary>Identifies the bounded set of operation kinds this policy governs.</summary>
    private static bool IsGovernedOperation(SecurityOperationKind kind) => kind is
        SecurityOperationKind.FileRead or SecurityOperationKind.DirectoryRead or SecurityOperationKind.FileSearch
        or SecurityOperationKind.FileWrite or SecurityOperationKind.DirectoryCreate;

    /// <summary>Determines whether one resource is a file or directory with a structurally safe relative identifier.</summary>
    private static bool IsSafeFileOrDirectoryResource(ProtectedResource resource) =>
        resource.Kind is ProtectedResourceKind.File or ProtectedResourceKind.Directory
        && IsSafeRelativePath(resource.Identifier);

    /// <summary>Rejects a rooted path (Unix-style or a Windows drive/UNC prefix, regardless of host OS) and any <c>..</c> segment.</summary>
    private static bool IsSafeRelativePath(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || Path.IsPathRooted(identifier) || IsWindowsStyleRooted(identifier))
        {
            return false;
        }

        var segments = identifier.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return !Array.Exists(segments, static segment => segment == "..");
    }

    /// <summary>Detects a Windows drive-letter or UNC prefix even when running on a non-Windows host.</summary>
    private static bool IsWindowsStyleRooted(string identifier) =>
        identifier.StartsWith(@"\\", StringComparison.Ordinal)
        || (identifier.Length >= 2 && char.IsAsciiLetter(identifier[0]) && identifier[1] == ':');
}

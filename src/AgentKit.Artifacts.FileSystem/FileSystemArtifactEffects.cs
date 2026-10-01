// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

/// <summary>Performs each protected file read and write the store needs, authorizing every effect under the captured authorization of the current operation.</summary>
/// <remarks>
/// Every effect passes one typed security request to the captured authority, receives an exact single-use grant, and presents it to
/// the selected reader or writer, which revalidates and consumes it. The store never opens a host path itself, never creates a
/// directory, and never chooses a disposition implicitly: each write names create-only, replace-existing, create-or-replace, or append.
/// </remarks>
internal sealed class FileSystemArtifactEffects
{
    private readonly FileSystemArtifactTarget _target;
    private readonly FileSystemArtifactSettings _settings;
    private readonly IFileSystemSelector _fileSystems;
    private readonly ISecurityAuthoritySelector _authorities;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly IIdentifierGenerator<FileOperationId> _fileOperationIds;
    private readonly TimeProvider _time;

    internal FileSystemArtifactEffects(
        FileSystemArtifactTarget target,
        FileSystemArtifactSettings settings,
        IFileSystemSelector fileSystems,
        ISecurityAuthoritySelector authorities,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        IIdentifierGenerator<FileOperationId> fileOperationIds,
        TimeProvider time)
    {
        Debug.Assert(target is not null, "The store validates its target.");
        Debug.Assert(settings is not null, "The store validates its settings.");
        Debug.Assert(fileSystems is not null, "The store validates its file-system selector.");
        Debug.Assert(authorities is not null, "The store validates its authority selector.");
        Debug.Assert(requestIds is not null, "The store validates its request identities.");
        Debug.Assert(fileOperationIds is not null, "The store validates its file-operation identities.");
        Debug.Assert(time is not null, "The store validates its clock.");
        _target = target;
        _settings = settings;
        _fileSystems = fileSystems;
        _authorities = authorities;
        _requestIds = requestIds;
        _fileOperationIds = fileOperationIds;
        _time = time;
    }

    /// <summary>Writes one payload under an explicit disposition.</summary>
    /// <param name="authorization">The captured authorization of the current operation.</param>
    /// <param name="name">The single-segment file name directly under the root.</param>
    /// <param name="payload">The exact bytes to write.</param>
    /// <param name="disposition">The explicit target-state disposition.</param>
    /// <param name="cancellationToken">Cancels before the effect.</param>
    /// <returns>The closed write outcome.</returns>
    /// <exception cref="FileEffectException">The writer or authority is unavailable, or authorization was refused.</exception>
    internal async ValueTask<FileWriteResult> WriteAsync(
        SecurityAuthorizationContext authorization,
        string name,
        ReadOnlyMemory<byte> payload,
        FileWriteDisposition disposition,
        CancellationToken cancellationToken)
    {
        var selection = await _fileSystems.SelectAsync(_target.ProfileKey, FileSystemCapability.Write, cancellationToken).ConfigureAwait(false);
        if (selection is not FileSystemWriterSelected writerSelected)
        {
            throw new FileEffectException("The configured file-system writer profile is unavailable.");
        }

        var relative = new NormalizedRelativePath(name);
        var target = FileHostTargetBinding.Target(_target.RootId, relative);
        var resolved = FileHostTargetBinding.Resolve(_target.RootId, relative, _target.HostRootPath);
        var payloadFingerprint = FileSecurityBinding.ContentFingerprint(payload.Span);
        var grant = await AuthorizeAsync(
            authorization,
            writerSelected.Writer.SecurityAudience,
            SecurityOperationKind.FileWrite,
            FileSecurityBinding.WriteEffect(disposition),
            FileSecurityBinding.Resource(target),
            FileSecurityBinding.WriteFingerprint(
                target, disposition, expectedTargetFingerprint: null, payload.Length, payloadFingerprint,
                FileWriteAtomicityMode.Required, FileWriteEffectClass.WorkspaceBytes, payloadFingerprint),
            cancellationToken).ConfigureAwait(false);
        var operation = new AuthorizedFileWrite(
            resolved, disposition, expectedTargetFingerprint: null, payload.Length, payloadFingerprint,
            FileWriteAtomicityMode.Required, FileWriteEffectClass.WorkspaceBytes, grant);
        return await writerSelected.Writer.WriteAsync(operation, new FileWriteContent(payload, payloadFingerprint), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads one complete file, bounded.</summary>
    /// <param name="authorization">The captured authorization of the current operation.</param>
    /// <param name="name">The single-segment file name directly under the root.</param>
    /// <param name="maximumBytes">The positive largest length accepted.</param>
    /// <param name="cancellationToken">Cancels before the effect.</param>
    /// <returns>The complete bytes, or <see langword="null"/> when the file does not exist.</returns>
    /// <exception cref="FileEffectException">The reader or authority is unavailable, authorization or the read was refused or failed, or the file exceeds <paramref name="maximumBytes"/>.</exception>
    internal async ValueTask<byte[]?> ReadAsync(
        SecurityAuthorizationContext authorization,
        string name,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var selection = await _fileSystems.SelectAsync(_target.ProfileKey, FileSystemCapability.Read, cancellationToken).ConfigureAwait(false);
        if (selection is not FileSystemReaderSelected readerSelected)
        {
            throw new FileEffectException("The configured file-system reader profile is unavailable.");
        }

        var relative = new NormalizedRelativePath(name);
        var target = FileHostTargetBinding.Target(_target.RootId, relative);
        var resolved = FileHostTargetBinding.Resolve(_target.RootId, relative, _target.HostRootPath);
        var runId = authorization.Scope.Correlation is InRunOperationCorrelation inRun ? (RunId?) inRun.RunId : null;
        var request = new FileReadRequest(
            _fileOperationIds.Create(), authorization.Scope.Correlation.OperationId, authorization.Scope.AgentId, runId, target,
            new FileReadBounds(maximumBytes + 1));
        var grant = await AuthorizeAsync(
            authorization,
            readerSelected.Reader.SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            FileSecurityBinding.Resource(target),
            FileSecurityBinding.ReadFingerprint(request),
            cancellationToken).ConfigureAwait(false);
        var opened = await readerSelected.Reader.OpenReadAsync(new AuthorizedFileRead(request, resolved, grant), cancellationToken).ConfigureAwait(false);
        switch (opened)
        {
            case FileReadOpenNotFound:
                return null;
            case FileReadHandleOpened handleOpened:
                await using (handleOpened.Handle.ConfigureAwait(false))
                {
                    using var buffer = new MemoryStream();
                    await handleOpened.Handle.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                    return buffer.Length > maximumBytes
                        ? throw new FileEffectException("A stored file exceeds its configured byte bound.")
                        : buffer.ToArray();
                }

            case FileReadOpenCancelled:
                throw new OperationCanceledException(cancellationToken);
            default:
                throw new FileEffectException("The file-system read was refused or failed.");
        }
    }

    private async ValueTask<SecurityGrant> AuthorizeAsync(
        SecurityAuthorizationContext authorization,
        ComponentId audience,
        SecurityOperationKind kind,
        SecurityEffect effect,
        ProtectedResource resource,
        InputFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        var activated = await _authorities.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (activated is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            throw new FileEffectException("The captured security authority is unavailable.");
        }

        var request = new SecurityRequest(
            _requestIds.Create(), authorization.Scope, null, authorization.Identity, authorization, audience, kind, effect, [resource],
            fingerprint, _time.GetUtcNow().Add(_settings.EffectAuthorizationLifetime));
        var decision = await selected.Authority.AuthorizeAsync(request, hooks: null, cancellationToken).ConfigureAwait(false);
        return decision is SecurityAllowed allowed
            ? allowed.Grant
            : throw new FileEffectException("The file-system effect was not authorized.");
    }
}

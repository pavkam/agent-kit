// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project;

/// <summary>Discovers bounded workspace instruction files and contributes them as instruction candidates.</summary>
/// <remarks>
/// Each candidate file is read through the <see cref="IFileReader"/> of the keyed file-system profile named by
/// <see cref="ProjectInstructionOptions.ProfileKey"/>, after the captured security authority allows a
/// <see cref="SecurityOperationKind.FileRead"/> for exactly that target. A missing, denied, oversized, or non-UTF-8 file
/// contributes no candidate; discovery never fails the request because one file is unreadable.
/// </remarks>
public sealed class ProjectInstructionContributor: IContextContributor
{
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly IFileSystemSelector _fileSystemSelector;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly IIdentifierGenerator<FileOperationId> _fileOperationIds;
    private readonly TimeProvider _timeProvider;
    private readonly ProjectInstructionOptions _options;
    private readonly ImmutableArray<NormalizedRelativePath> _instructionPaths;

    /// <summary>Initializes the contributor.</summary>
    /// <param name="fileSystemSelector">Selects the keyed file-system profile that provides the protected reader.</param>
    /// <param name="authoritySelector">The selector used to resolve the captured authority for each read.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="fileOperationIds">The file-operation identity generator.</param>
    /// <param name="timeProvider">The clock used to bound authorization.</param>
    /// <param name="options">Validated discovery options captured at construction.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <exception cref="ArgumentException">A search root or filename is blank, the host root is blank, or a search root and filename do not form a valid relative path.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
    public ProjectInstructionContributor(
        IFileSystemSelector fileSystemSelector,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        IIdentifierGenerator<FileOperationId> fileOperationIds,
        TimeProvider timeProvider,
        IOptions<ProjectInstructionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(fileSystemSelector);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(fileOperationIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaxBytesPerFile, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.HostRootPath, nameof(options));
        ArgumentNullException.ThrowIfNull(options.Value.SearchRoots);
        ArgumentNullException.ThrowIfNull(options.Value.InstructionFilenames);
        foreach (var root in options.Value.SearchRoots)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root);
        }

        var paths = ImmutableArray.CreateBuilder<NormalizedRelativePath>();
        foreach (var filename in options.Value.InstructionFilenames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        }

        foreach (var root in options.Value.SearchRoots)
        {
            foreach (var filename in options.Value.InstructionFilenames)
            {
                paths.Add(CombineRelativePath(root, filename));
            }
        }

        _fileSystemSelector = fileSystemSelector;
        _authoritySelector = authoritySelector;
        _requestIds = requestIds;
        _fileOperationIds = fileOperationIds;
        _timeProvider = timeProvider;
        _options = options.Value;
        _instructionPaths = paths.ToImmutable();
    }

    /// <inheritdoc/>
    public async ValueTask<ContextContribution> ContributeAsync(
        ContextContributionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = ImmutableArray.CreateBuilder<ContextCandidate>();
        foreach (var path in _instructionPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = await TryReadInstructionCandidateAsync(request, path, cancellationToken).ConfigureAwait(false);
            if (candidate is not null)
            {
                candidates.Add(candidate);
            }
        }

        return new ContextContribution(candidates.ToImmutable(), []);
    }

    private async Task<ContextCandidate?> TryReadInstructionCandidateAsync(
        ContextContributionRequest request,
        NormalizedRelativePath path,
        CancellationToken cancellationToken)
    {
        var selection = await _fileSystemSelector
            .SelectAsync(_options.ProfileKey, FileSystemCapability.Read, cancellationToken)
            .ConfigureAwait(false);
        if (selection is not FileSystemReaderSelected readerSelected)
        {
            return null;
        }

        var authorization = request.Authorization;
        var target = FileHostTargetBinding.Target(_options.RootId, path);
        var readRequest = new FileReadRequest(
            _fileOperationIds.Create(),
            authorization.Scope.Correlation.OperationId,
            request.Agent.Id,
            request.RunId,
            target,
            new FileReadBounds(_options.MaxBytesPerFile));
        var securityRequest = new SecurityRequest(
            _requestIds.Create(),
            authorization.Scope,
            toolCallId: null,
            authorization.Identity,
            authorization,
            readerSelected.Reader.SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(target)],
            FileSecurityBinding.ReadFingerprint(readRequest),
            _timeProvider.GetUtcNow().AddMinutes(1));
        var activated = await _authoritySelector.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        if (activated is not SecurityAuthoritySelected selected || selected.Authorization != authorization)
        {
            return null;
        }

        var decision = await selected.Authority.AuthorizeAsync(securityRequest, hooks: null, cancellationToken).ConfigureAwait(false);
        if (decision is not SecurityAllowed allowed)
        {
            return null;
        }

        var resolved = FileHostTargetBinding.Resolve(_options.RootId, path, _options.HostRootPath);
        var opened = await readerSelected.Reader
            .OpenReadAsync(new AuthorizedFileRead(readRequest, resolved, allowed.Grant), cancellationToken)
            .ConfigureAwait(false);
        if (opened is not FileReadHandleOpened handleOpened)
        {
            return null;
        }

        await using var handle = handleOpened.Handle;
        if (handle.Metadata.LengthBytes > _options.MaxBytesPerFile)
        {
            return null;
        }

        string text;
        try
        {
            using var reader = new StreamReader(handle.Content, _strictUtf8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetByteCount(text);
        return new ContextCandidate(
            new ContextSourceReference(
                new ContextSourceNamespace("agentkit.context.project"),
                new ContextSourceKey($"project-instructions:{path}"),
                new ContextSourceVersion("1")),
            ContextCandidateKind.Instruction,
            ContextTrust.Workspace,
            priority: 100,
            ContextScope.Run,
            new ContextCostEstimate(bytes, Math.Max(1, bytes / 4)),
            ContextFreshness.Pinned,
            ContextEvaluationFrequency.OncePerRun,
            mandatory: false,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
    }

    private static NormalizedRelativePath CombineRelativePath(string root, string filename)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(root), "Search roots are validated before combination.");
        Debug.Assert(!string.IsNullOrWhiteSpace(filename), "Instruction filenames are validated before combination.");
        var trimmed = root.TrimEnd('/');
        var combined = trimmed.Length == 0 || root is "."
            ? filename
            : $"{trimmed}/{filename}";
        return new NormalizedRelativePath(combined);
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project;

#pragma warning disable CS0612 // Legacy IFileSystem until host tools migrate onto IFileReader.

/// <summary>Discovers bounded workspace instruction files and contributes them as instruction candidates.</summary>
public sealed class ProjectInstructionContributor: IContextContributor
{
    [Obsolete("Use IFileReader once context discovery selects a keyed file-system profile.")]
    private readonly IFileSystem _fileSystem;
    private readonly ISecurityAuthoritySelector _authoritySelector;
    private readonly IIdentifierGenerator<SecurityRequestId> _requestIds;
    private readonly TimeProvider _timeProvider;
    private readonly ProjectInstructionOptions _options;

    /// <summary>Initializes the contributor.</summary>
    /// <param name="fileSystem">The protected file-system boundary used for reads.</param>
    /// <param name="authoritySelector">The selector used to resolve the captured authority for each read.</param>
    /// <param name="requestIds">The security-request identity generator.</param>
    /// <param name="timeProvider">The clock used to bound authorization.</param>
    /// <param name="options">Validated discovery options captured at construction.</param>
    /// <exception cref="ArgumentNullException">Any dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
    [Obsolete("Use IFileReader once context discovery selects a keyed file-system profile.")]
    public ProjectInstructionContributor(
        IFileSystem fileSystem,
        ISecurityAuthoritySelector authoritySelector,
        IIdentifierGenerator<SecurityRequestId> requestIds,
        TimeProvider timeProvider,
        IOptions<ProjectInstructionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(authoritySelector);
        ArgumentNullException.ThrowIfNull(requestIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaxBytesPerFile, nameof(options));
        ArgumentNullException.ThrowIfNull(options.Value.SearchRoots);
        ArgumentNullException.ThrowIfNull(options.Value.InstructionFilenames);
        foreach (var root in options.Value.SearchRoots)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root);
        }

        foreach (var filename in options.Value.InstructionFilenames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        }
        _fileSystem = fileSystem;
        _authoritySelector = authoritySelector;
        _requestIds = requestIds;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <inheritdoc/>
    [Obsolete]
    public async ValueTask<ContextContribution> ContributeAsync(
        ContextContributionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = ImmutableArray.CreateBuilder<ContextCandidate>();
        foreach (var root in _options.SearchRoots)
        {
            foreach (var filename in _options.InstructionFilenames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = CombineRelativePath(root, filename);
                var candidate = await TryReadInstructionCandidateAsync(request, relativePath, cancellationToken).ConfigureAwait(false);
                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }
        }

        return new ContextContribution(candidates.ToImmutable(), []);
    }

    [Obsolete("Use IFileReader once context discovery selects a keyed file-system profile.")]
    private async Task<ContextCandidate?> TryReadInstructionCandidateAsync(
        ContextContributionRequest request,
        FileSystemPath path,
        CancellationToken cancellationToken)
    {
        var authorization = request.Authorization;
        var securityRequest = new SecurityRequest(
            _requestIds.Create(),
            authorization.Scope,
            toolCallId: null,
            authorization.Identity,
            authorization,
            _fileSystem.SecurityAudience,
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [FileSecurityBinding.Resource(path)],
            FileSecurityBinding.ReadFingerprint(path),
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

        var readResult = await _fileSystem.ReadAsync(new LegacyFileReadRequest(path, allowed.Grant), cancellationToken).ConfigureAwait(false);
        if (readResult is not FileRead read || read.Bytes > _options.MaxBytesPerFile)
        {
            return null;
        }

        var text = read.Content;
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

    private static FileSystemPath CombineRelativePath(string root, string filename)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        var combined = root.TrimEnd('/').Length == 0 || root is "."
            ? filename
            : $"{root.TrimEnd('/')}/{filename}";
        return new FileSystemPath(combined);
    }
}

#pragma warning restore CS0612

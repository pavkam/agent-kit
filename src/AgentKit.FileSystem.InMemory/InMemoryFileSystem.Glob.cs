// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.InMemory;

public sealed partial class InMemoryFileSystem
{
    /// <inheritdoc/>
    private async ValueTask<GlobResult> GlobCoreAsync(GlobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Pattern.Value, "request.Pattern");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumVisitedEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumResults);
        ArgumentNullException.ThrowIfNull(request.Grant);
        cancellationToken.ThrowIfCancellationRequested();

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.DirectoryRead,
            SecurityEffect.Observe,
            [GlobSecurityBinding.Resource(request.BasePath)],
            GlobSecurityBinding.Fingerprint(
                request.BasePath,
                request.Pattern,
                request.CaseSensitive,
                request.IncludeHidden,
                request.MaximumDepth,
                request.MaximumVisitedEntries,
                request.MaximumResults));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(request.Grant, enforcement, intent, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, intent))
        {
            return new GlobResult(GlobStatus.Denied, [], 0, false, FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        lock (_gate)
        {
            var basePath = request.BasePath?.Value;
            if (!DirectoryExists(basePath))
            {
                return _files.ContainsKey(basePath ?? "")
                    ? new GlobResult(GlobStatus.Denied, [], 0, false, "The glob base crosses an inaccessible boundary.")
                    : new GlobResult(GlobStatus.NotFound, [], 0, true, "The glob base directory does not exist.");
            }

            var state = new GlobTraversalState(request);
            TraverseGlobDirectory(basePath, 1, state, cancellationToken);
            state.Matches.Sort(StringComparer.Ordinal);
            var matches = state.Matches.Select(static value => new FileSystemPath(value)).ToImmutableArray();
            return state.TerminalStatus is { } terminal
                ? new GlobResult(terminal, matches, state.VisitedEntries, false, state.SafeMessage)
                : matches.IsEmpty
                    ? new GlobResult(GlobStatus.NoMatches, [], state.VisitedEntries, true, "The glob completed with no matches.")
                    : new GlobResult(GlobStatus.Success, matches, state.VisitedEntries, true, null);
        }
    }

    private void TraverseGlobDirectory(
        string? directoryPath,
        int depth,
        GlobTraversalState state,
        CancellationToken cancellationToken)
    {
        if (state.TerminalStatus is not null)
        {
            return;
        }

        foreach (var name in ChildNames(directoryPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!state.Request.IncludeHidden && name.StartsWith('.'))
            {
                continue;
            }

            state.VisitedEntries++;
            if (state.VisitedEntries > state.Request.MaximumVisitedEntries)
            {
                state.Fail(GlobStatus.LimitExceeded, "The glob visited-entry limit was exceeded.");
                return;
            }

            var relative = directoryPath is null ? name : $"{directoryPath}/{name}";
            var workspacePath = state.Request.BasePath is null ? relative : $"{state.Request.BasePath.Value.Value}/{relative}";
            if (GlobMatches(state.Request.Pattern.Value, relative, state.Request.CaseSensitive))
            {
                if (state.Matches.Count == state.Request.MaximumResults)
                {
                    state.Fail(GlobStatus.LimitExceeded, "The glob retained-result limit was exceeded.");
                    return;
                }

                state.Matches.Add(workspacePath);
            }

            if (_directories.Contains(relative) && depth < state.Request.MaximumDepth)
            {
                TraverseGlobDirectory(relative, depth + 1, state, cancellationToken);
                if (state.TerminalStatus is not null)
                {
                    return;
                }
            }
        }
    }

    /// <summary>Enumerates direct child names of a directory, ordinal-sorted.</summary>
    private List<string> ChildNames(string? directoryPath)
    {
        var names = new List<string>();
        foreach (var candidate in _directories.Concat(_files.Keys))
        {
            if (ParentDirectory(candidate) == directoryPath)
            {
                names.Add(ChildName(candidate));
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static bool GlobMatches(string pattern, string path, bool caseSensitive)
    {
        var patternSegments = pattern.Split('/');
        var pathSegments = path.Split('/');
        return MatchGlobSegments(patternSegments, 0, pathSegments, 0, caseSensitive);
    }

    private static bool MatchGlobSegments(
        string[] pattern,
        int patternIndex,
        string[] path,
        int pathIndex,
        bool caseSensitive)
    {
        return patternIndex == pattern.Length
            ? pathIndex == path.Length
            : pattern[patternIndex] == "**"
                ? MatchGlobSegments(pattern, patternIndex + 1, path, pathIndex, caseSensitive)
                    || (pathIndex < path.Length
                        && MatchGlobSegments(pattern, patternIndex, path, pathIndex + 1, caseSensitive))
                : pathIndex < path.Length
                    && System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                        pattern[patternIndex], path[pathIndex], ignoreCase: !caseSensitive)
                    && MatchGlobSegments(pattern, patternIndex + 1, path, pathIndex + 1, caseSensitive);
    }

    private sealed class GlobTraversalState(GlobRequest request)
    {
        public GlobRequest Request { get; } = request;
        public List<string> Matches { get; } = [];
        public int VisitedEntries { get; set; }
        public GlobStatus? TerminalStatus { get; private set; }
        public string? SafeMessage { get; private set; }

        public void Fail(GlobStatus status, string message)
        {
            TerminalStatus = status;
            SafeMessage = message;
        }
    }
}

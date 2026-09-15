// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Microsoft.Win32.SafeHandles;

public sealed partial class SandboxedFileSystem
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    /// <inheritdoc/>
    private async ValueTask<FileSearchResult> SearchCoreAsync(
        FileSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Pattern.Value, "request.Pattern");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PathPattern.Value, "request.PathPattern");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumFiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumMatches);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaximumLineBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(request.MaximumDuration, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(request.Grant);
        cancellationToken.ThrowIfCancellationRequested();

        var enforcement = FileSystemEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            SecurityOperationKind.FileSearch,
            SecurityEffect.Observe,
            [FileSearchSecurityBinding.Resource(request.BasePath)],
            FileSearchSecurityBinding.Fingerprint(
                request.BasePath,
                request.Pattern,
                request.PathPattern,
                request.CaseSensitive,
                request.IncludeHidden,
                request.MaximumDepth,
                request.MaximumFiles,
                request.MaximumBytes,
                request.MaximumMatches,
                request.MaximumLineBytes,
                request.MaximumDuration,
                request.ExcludedPathPatterns));
        var enforcementIntent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(
            request.Grant, enforcement, enforcementIntent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!FileSystemEnforcementReceipt.IsFreshExact(grantResult, request.Grant, enforcement, enforcementIntent))
        {
            return Failure(FileSearchStatus.Denied, FileSystemEnforcementReceipt.DenialMessage(grantResult));
        }

        if (request.MaximumDepth > _maximumSearchDepth
            || request.MaximumFiles > _maximumSearchFiles
            || request.MaximumBytes > _maximumSearchBytes
            || request.MaximumMatches > _maximumSearchMatches
            || request.MaximumLineBytes > _maximumSearchLineBytes
            || request.MaximumDuration > _maximumSearchDuration)
        {
            return Failure(FileSearchStatus.Denied, "The search exceeds a configured host boundary.");
        }

        if (!IsSecureTraversalSupported)
        {
            return Failure(FileSearchStatus.Denied, "Secure no-follow traversal is unavailable on this platform.");
        }

        if (!TryOpenDirectory(request.BasePath, cancellationToken, out var root, out var openError))
        {
            return openError == _errorNotFound
                ? Failure(FileSearchStatus.NotFound, "The search base directory does not exist.", complete: true)
                : Failure(
                    IsBoundaryViolation(openError) ? FileSearchStatus.Denied : FileSearchStatus.Failed,
                    IsBoundaryViolation(openError)
                        ? "The search base crosses a symbolic link or inaccessible boundary."
                        : "The search base could not be traversed.");
        }

        using (root)
        {
            var state = new SearchTraversalState(request, _timeProvider);
            await TraverseSearchDirectoryAsync(root, "", 1, state, cancellationToken).ConfigureAwait(false);
            var matches = state.Matches.ToImmutableArray();
            return state.TerminalStatus is { } terminal
                ? new FileSearchResult(
                    terminal, matches, state.VisitedFiles, state.VisitedBytes, false, state.SafeMessage)
                : matches.IsEmpty
                    ? new FileSearchResult(
                        FileSearchStatus.NoMatches,
                        [],
                        state.VisitedFiles,
                        state.VisitedBytes,
                        true,
                        "The search completed with no matches.")
                    : new FileSearchResult(
                        FileSearchStatus.Success, matches, state.VisitedFiles, state.VisitedBytes, true, null);
        }
    }

    private static async ValueTask TraverseSearchDirectoryAsync(
        SafeFileHandle directory,
        string relativeParent,
        int depth,
        SearchTraversalState state,
        CancellationToken cancellationToken)
    {
        if (state.ShouldStop())
        {
            return;
        }

        if (!TryReadDirectoryNames(directory, cancellationToken, out var names, out _))
        {
            state.Fail(FileSearchStatus.Failed, "A directory could not be enumerated.");
            return;
        }

        names.Sort(StringComparer.Ordinal);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state.ShouldStop())
            {
                return;
            }

            if (!state.Request.IncludeHidden && name.StartsWith('.'))
            {
                continue;
            }

            var relative = relativeParent.Length == 0 ? name : $"{relativeParent}/{name}";
            if (IsExcludedPath(relative, state.Request.ExcludedPathPatterns, caseSensitive: true))
            {
                continue;
            }

            var directoryDescriptor = OpenAt(
                directory.DangerousGetHandle().ToInt32(),
                name,
                _openReadOnly | DirectoryFlag | NoFollowFlag | CloseOnExecFlag,
                0);
            if (directoryDescriptor >= 0)
            {
                using var child = new SafeFileHandle(new IntPtr(directoryDescriptor), ownsHandle: true);
                if (depth < state.Request.MaximumDepth)
                {
                    await TraverseSearchDirectoryAsync(
                        child, relative, depth + 1, state, cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            var directoryError = Marshal.GetLastPInvokeError();
            if (directoryError != _errorNotDirectory)
            {
                if (IsTooManyLinks(directoryError))
                {
                    continue;
                }

                if (IsBoundaryViolation(directoryError))
                {
                    state.Fail(FileSearchStatus.Denied, "A directory entry crossed an inaccessible boundary.");
                    return;
                }
            }

            if (!GlobMatches(state.Request.PathPattern.Value, relative, caseSensitive: true))
            {
                continue;
            }

            await SearchFileAsync(directory, name, relative, state, cancellationToken).ConfigureAwait(false);
            if (state.TerminalStatus is not null)
            {
                return;
            }
        }
    }

    private static async ValueTask SearchFileAsync(
        SafeFileHandle directory,
        string name,
        string relative,
        SearchTraversalState state,
        CancellationToken cancellationToken)
    {
        var descriptor = OpenAt(
            directory.DangerousGetHandle().ToInt32(),
            name,
            _openReadOnly | NoFollowFlag | CloseOnExecFlag | NonBlockingFlag,
            0);
        if (descriptor < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            if (!IsTooManyLinks(error) && IsBoundaryViolation(error))
            {
                state.Fail(FileSearchStatus.Denied, "A candidate file crossed an inaccessible boundary.");
            }

            return;
        }

        using var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        if (state.VisitedFiles == state.Request.MaximumFiles)
        {
            state.Fail(FileSearchStatus.LimitExceeded, "The search candidate-file limit was reached.");
            return;
        }

        state.VisitedFiles++;
        await using var stream = new FileStream(handle, FileAccess.Read, bufferSize: 81920, isAsync: false);
        long length;
        try
        {
            length = stream.Length;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            return;
        }

        var remaining = state.Request.MaximumBytes - state.VisitedBytes;
        if (length > remaining || length > int.MaxValue)
        {
            state.Fail(FileSearchStatus.LimitExceeded, "The search observed-byte limit was reached.");
            return;
        }

        var bytes = new byte[checked((int) length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        if (stream.Length != length)
        {
            state.Fail(FileSearchStatus.Failed, "A candidate file changed while it was being searched.");
            return;
        }

        state.VisitedBytes += length;
        if (bytes.AsSpan().Contains((byte) 0))
        {
            return;
        }

        try
        {
            _ = _strictUtf8.GetCharCount(bytes);
        }
        catch (DecoderFallbackException)
        {
            return;
        }

        var workspacePath = state.Request.BasePath is null
            ? relative
            : $"{state.Request.BasePath.Value.Value}/{relative}";
        var fingerprint = new ContentHash(
            $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}");
        MatchFileLines(bytes, new FileSystemPath(workspacePath), fingerprint, state);
    }

    private static void MatchFileLines(
        byte[] bytes,
        FileSystemPath path,
        ContentHash fingerprint,
        SearchTraversalState state)
    {
        var lineNumber = 1;
        var lineStart = 0;
        while (lineStart <= bytes.Length)
        {
            if (state.ShouldStop())
            {
                return;
            }

            var relativeLf = bytes.AsSpan(lineStart).IndexOf((byte) '\n');
            var lineEnd = relativeLf < 0 ? bytes.Length : lineStart + relativeLf;
            var contentEnd = lineEnd > lineStart && bytes[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;
            var lineBytes = bytes.AsSpan(lineStart, contentEnd - lineStart);
            var decodedLine = _strictUtf8.GetString(lineBytes);
            foreach (var (characterOffset, characterLength) in state.FindMatches(decodedLine))
            {
                if (state.Matches.Count == state.Request.MaximumMatches)
                {
                    state.Fail(FileSearchStatus.LimitExceeded, "The search retained-match limit was reached.");
                    return;
                }

                var matchByteOffset = _strictUtf8.GetByteCount(decodedLine.AsSpan(0, characterOffset));
                var matchByteLength = _strictUtf8.GetByteCount(
                    decodedLine.AsSpan(characterOffset, characterLength));
                var availableContext = Math.Max(0, state.Request.MaximumLineBytes - matchByteLength);
                var projectionOffset = Math.Max(0, matchByteOffset - (availableContext / 2));
                while (projectionOffset < matchByteOffset
                    && (lineBytes[projectionOffset] & 0xC0) == 0x80)
                {
                    projectionOffset++;
                }

                var projectionLength = Math.Min(
                    lineBytes.Length - projectionOffset, state.Request.MaximumLineBytes);
                var projectionBytes = lineBytes.Slice(projectionOffset, projectionLength);
                var projectionText = "";
                while (projectionBytes.Length > 0)
                {
                    try
                    {
                        projectionText = _strictUtf8.GetString(projectionBytes);
                        break;
                    }
                    catch (DecoderFallbackException)
                    {
                        projectionBytes = projectionBytes[..^1];
                    }
                }

                state.Matches.Add(new FileSearchMatch(
                    path,
                    fingerprint,
                    lineNumber,
                    lineStart,
                    matchByteOffset,
                    matchByteLength,
                    projectionOffset,
                    projectionText,
                    projectionBytes.Length < lineBytes.Length));
            }

            if (relativeLf < 0)
            {
                return;
            }

            lineStart = lineEnd + 1;
            lineNumber++;
        }
    }

    private static bool IsTooManyLinks(int error) => error == (OperatingSystem.IsMacOS()
        ? _macOsErrorTooManyLinks
        : _linuxErrorTooManyLinks);

    private static int NonBlockingFlag => OperatingSystem.IsMacOS() ? 0x0004 : 0x0800;

    private static FileSearchResult Failure(
        FileSearchStatus status,
        string message,
        bool complete = false) => new(status, [], 0, 0, complete, message);

    private sealed class SearchTraversalState
    {
        private readonly long _started;
        private readonly TimeProvider _timeProvider;
        private readonly Regex? _regex;

        public SearchTraversalState(FileSearchRequest request, TimeProvider timeProvider)
        {
            Request = request;
            _timeProvider = timeProvider;
            _started = timeProvider.GetTimestamp();
            if (request.Pattern.Kind == FileSearchPatternKind.RegularExpression)
            {
                _regex = new Regex(
                    request.Pattern.Value,
                    RegexOptions.CultureInvariant
                        | RegexOptions.NonBacktracking
                        | (request.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase));
            }
        }

        public FileSearchRequest Request { get; }
        public List<FileSearchMatch> Matches { get; } = [];
        public int VisitedFiles { get; set; }
        public long VisitedBytes { get; set; }
        public FileSearchStatus? TerminalStatus { get; private set; }
        public string? SafeMessage { get; private set; }

        public bool ShouldStop()
        {
            if (TerminalStatus is not null)
            {
                return true;
            }

            if (_timeProvider.GetElapsedTime(_started) < Request.MaximumDuration)
            {
                return false;
            }

            Fail(FileSearchStatus.TimedOut, "The search duration limit elapsed.");
            return true;
        }

        public IEnumerable<(int Offset, int Length)> FindMatches(string text)
        {
            if (_regex is not null)
            {
                foreach (Match match in _regex.Matches(text))
                {
                    yield return (match.Index, match.Length);
                }

                yield break;
            }

            var comparison = Request.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
            var offset = 0;
            while (offset <= text.Length - Request.Pattern.Value.Length)
            {
                var found = text.IndexOf(Request.Pattern.Value, offset, comparison);
                if (found < 0)
                {
                    yield break;
                }

                yield return (found, Request.Pattern.Value.Length);
                offset = found + Math.Max(1, Request.Pattern.Value.Length);
            }
        }

        public void Fail(FileSearchStatus status, string message)
        {
            TerminalStatus = status;
            SafeMessage = message;
        }
    }
}

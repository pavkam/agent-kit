// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List.Tests;

using AgentKit.TestSupport;
using AgentKit.Tools.List;

internal static class TestListComposition
{
    internal static IFileSystemSelector CreateSelector(FileSystemProfileKey key, IDirectoryReader reader) =>
        new TestListFileSystemSelector(key, reader);

    internal static ListDirectoryTool CreateTool(
            IDirectoryReader reader,
            ISecurityAuthority authority,
            ListDirectoryToolOptions? options = null,
            ILogger<ListDirectoryTool>? logger = null)
    {
        var profileKey = options?.ProfileKey ?? new FileSystemProfileKey("test");
        var configured = options ?? new ListDirectoryToolOptions
        {
            ProfileKey = profileKey,
            RootId = new FileRootId("test"),
            HostRootPath = "/tmp/test-root",
        };

        return new ListDirectoryTool(
            new TestListFileSystemSelector(profileKey, reader),
            new TestPathNormalizer(),
            new FixedSecurityAuthoritySelector(authority),
            new StubSecurityRequestIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(configured),
            logger ?? NullLogger<ListDirectoryTool>.Instance);
    }

    private sealed class TestListFileSystemSelector(FileSystemProfileKey key, IDirectoryReader reader): IFileSystemSelector
    {
        private static readonly FileSystemCapabilities _capabilities = new(FileSystemCapability.Enumerate);

        public ValueTask<FileSystemSelectionResult> SelectAsync(
            FileSystemProfileKey requestedKey,
            FileSystemCapability requiredCapability,
            CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return requestedKey == key && requiredCapability is FileSystemCapability.Enumerate
                ? ValueTask.FromResult<FileSystemSelectionResult>(
                    new FileSystemDirectoryReaderSelected(key, reader, _capabilities))
                : ValueTask.FromResult<FileSystemSelectionResult>(
                    new FileSystemCapabilityUnsupported(key, requiredCapability, _capabilities));
        }
    }

    private sealed class TestPathNormalizer: IFilePathNormalizer
    {
        public FilePathNormalizationResult Normalize(FilePathInput input, FilePathPolicy policy)
        {
            _ = policy;
            try
            {
                return new FilePathNormalizationSuccess(new NormalizedRelativePath(input.RelativePath));
            }
            catch (ArgumentException exception)
            {
                return new FilePathNormalizationFailed(exception.Message);
            }
        }
    }
}

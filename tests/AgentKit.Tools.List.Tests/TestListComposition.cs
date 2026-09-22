// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List.Tests;

using AgentKit.TestSupport;
using AgentKit.Tools.List;

using Microsoft.Extensions.DependencyInjection;

internal static class TestListComposition
{
    internal static IFileSystemSelector CreateSelector(FileSystemProfileKey key) =>
        new TestListFileSystemSelector(key);

    [Obsolete("Legacy host surface.")]
    internal static ListDirectoryTool CreateTool(
            ILegacyDirectoryReader reader,
            ISecurityAuthority authority,
            ListDirectoryToolOptions? options = null)
    {
        var profileKey = options?.ProfileKey ?? new FileSystemProfileKey("test");
        var configured = options ?? new ListDirectoryToolOptions
        {
            ProfileKey = profileKey,
            RootId = new FileRootId("test"),
            HostRootPath = "/tmp/test-root",
        };

        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton(profileKey.Value, reader);
        _ = services.AddKeyedSingleton(profileKey.Value, reader);
        _ = services.AddSingleton<IFileSystemSelector>(new TestListFileSystemSelector(profileKey));
        var provider = services.BuildServiceProvider();
        return new ListDirectoryTool(
            provider.GetRequiredService<IFileSystemSelector>(),
            provider,
            new TestPathNormalizer(),
            new FixedSecurityAuthoritySelector(authority),
            new StubSecurityRequestIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(configured));
    }

    private sealed class TestListFileSystemSelector(FileSystemProfileKey key): IFileSystemSelector
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
                    new FileSystemDirectoryReaderSelected(key, new EmptyDirectoryReader(), _capabilities))
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

    private sealed class EmptyDirectoryReader: IDirectoryReader
    {
        public IAsyncEnumerable<FileSystemEntry> EnumerateAsync(
            AuthorizedDirectoryEnumeration operation,
            CancellationToken cancellationToken = default) =>
            AsyncEnumerable.Empty<FileSystemEntry>();
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddSandboxedFileSystem_WhenCalled_RegistersFileSystemWithConfiguredRoot()
    {
        var services = new ServiceCollection();
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));

        _ = services.AddSandboxedFileSystem(root);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IFileSystem>().ShouldBeOfType<SandboxedFileSystem>();
        provider.GetRequiredService<IOptions<SandboxedFileSystemOptions>>().Value.RootDirectory.ShouldBe(root);
    }

    [Fact]
    public void AddSandboxedFileSystem_WhenCalledTwice_KeepsFirstServiceRegistration()
    {
        var services = new ServiceCollection();
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));

        _ = services.AddSandboxedFileSystem(root);
        _ = services.AddSandboxedFileSystem(root);
        using var provider = services.BuildServiceProvider();

        provider.GetServices<IFileSystem>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddSandboxedFileSystem_WhenRootDirectoryBlank_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        _ = Should.Throw<ArgumentException>(() => services.AddSandboxedFileSystem("   "));
    }

    [Fact]
    public void AddSandboxedFileSystem_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));

        _ = services.AddSandboxedFileSystem(root, o => o.MaximumReadBytes = 123);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<SandboxedFileSystemOptions>>().Value.MaximumReadBytes.ShouldBe(123L);
    }

    [Fact]
    public void AddSandboxedFileSystem_WhenMaximumReadBytesIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        var root = Path.Combine(Path.GetTempPath(), "agentkit-fs-di-" + Guid.NewGuid().ToString("N"));
        _ = services.AddSandboxedFileSystem(root, o => o.MaximumReadBytes = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<SandboxedFileSystemOptions>>().Value);
    }
}

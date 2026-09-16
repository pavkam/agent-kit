// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies ResolvedProcessIntent behavior and contracts.</summary>
public sealed class ResolvedProcessIntentTests
{
    [Fact]
    public void Constructor_WhenRequestIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ResolvedProcessIntent(
            null!, "/usr/bin/sh", new ContentHash("sha256:executable"), "/workspace", "/workspace",
            new ContentHash("sha256:environment"), new ContentHash("sha256:stdin"))).ParamName.ShouldBe("request");

    [Fact]
    public void Constructor_WhenExecutablePathIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Intent(executablePath: " ")).ParamName.ShouldBe("absoluteExecutablePath");

    [Fact]
    public void Constructor_WhenExecutablePathIsRelative_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Intent(executablePath: "relative/sh")).ParamName.ShouldBe("absoluteExecutablePath");

    [Fact]
    public void Constructor_WhenWorkspaceRootIsRelative_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Intent(workspaceRoot: "relative")).ParamName.ShouldBe("absoluteWorkspaceRoot");

    [Fact]
    public void Constructor_WhenWorkingDirectoryIsRelative_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Intent(workingDirectory: "relative")).ParamName.ShouldBe("absoluteWorkingDirectory");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var request = HostTestData.ResolveRequest();
        var intent = new ResolvedProcessIntent(
            request, "/usr/bin/sh", new ContentHash("sha256:executable"), "/workspace", "/workspace/child",
            new ContentHash("sha256:environment"), new ContentHash("sha256:stdin"));
        intent.Request.ShouldBeSameAs(request);
        intent.AbsoluteExecutablePath.ShouldBe("/usr/bin/sh");
        intent.ExecutableFingerprint.ShouldBe(new ContentHash("sha256:executable"));
        intent.AbsoluteWorkspaceRoot.ShouldBe("/workspace");
        intent.AbsoluteWorkingDirectory.ShouldBe("/workspace/child");
        intent.EnvironmentFingerprint.ShouldBe(new ContentHash("sha256:environment"));
        intent.StandardInputFingerprint.ShouldBe(new ContentHash("sha256:stdin"));
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Intent();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ResolvedProcessIntent Intent(
        ProcessResolveRequest? request = null,
        string executablePath = "/usr/bin/sh",
        string workspaceRoot = "/workspace",
        string workingDirectory = "/workspace") =>
        new(
            request ?? HostTestData.ResolveRequest(),
            executablePath,
            new ContentHash("sha256:executable"),
            workspaceRoot,
            workingDirectory,
            new ContentHash("sha256:environment"),
            new ContentHash("sha256:stdin"));
}

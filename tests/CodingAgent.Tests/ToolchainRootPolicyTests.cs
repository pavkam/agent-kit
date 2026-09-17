// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

public sealed class ToolchainRootPolicyTests
{
    private static readonly string Root = Path.GetPathRoot(Path.GetTempPath())!;
    private static readonly string Home = Path.Combine(Root, "home", "alex");
    private static readonly string Workspace = Path.Combine(Home, "dev", "repo");
    private static readonly string Homebrew = Path.Combine(Root, "opt", "homebrew");

    [Fact]
    public void Validate_WhenPathIsAbsoluteAndExists_AcceptsTheNormalizedPath()
    {
        var outcome = ToolchainRootPolicy.Validate(
            $"  {Homebrew}{Path.DirectorySeparatorChar}  ",
            Workspace,
            [],
            Home,
            static _ => true);

        outcome.IsAccepted.ShouldBeTrue();
        outcome.Path.ShouldBe(Homebrew);
        outcome.Error.ShouldBeNull();
    }

    [Fact]
    public void Validate_WhenPathStartsWithTilde_ExpandsTheHomeDirectory()
    {
        var outcome = ToolchainRootPolicy.Validate("~/.dotnet", Workspace, [], Home, static _ => true);

        outcome.Path.ShouldBe(Path.Combine(Home, ".dotnet"));
    }

    [Fact]
    public void Validate_WhenPathIsOnlyTildeAndHomeContainsWorkspace_RejectsAsTooBroad()
    {
        var outcome = ToolchainRootPolicy.Validate("~", Workspace, [], Home, static _ => true);

        outcome.IsAccepted.ShouldBeFalse();
        outcome.Error.ShouldNotBeNull().ShouldContain("contains the workspace");
    }

    [Fact]
    public void Validate_WhenHomeIsNull_DoesNotExpandTilde()
    {
        var outcome = ToolchainRootPolicy.Validate("~/.dotnet", Workspace, [], null, static _ => true);

        outcome.IsAccepted.ShouldBeFalse();
        outcome.Error.ShouldNotBeNull().ShouldContain("absolute");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenInputIsBlank_AsksForAPath(string input)
    {
        var outcome = ToolchainRootPolicy.Validate(input, Workspace, [], Home, static _ => true);

        outcome.IsAccepted.ShouldBeFalse();
        outcome.Error.ShouldBe("Enter a folder path.");
    }

    [Fact]
    public void Validate_WhenPathIsRelative_RequiresAnAbsolutePath()
    {
        var outcome = ToolchainRootPolicy.Validate("tools/bin", Workspace, [], Home, static _ => true);

        outcome.IsAccepted.ShouldBeFalse();
        outcome.Error.ShouldNotBeNull().ShouldContain("absolute");
    }

    [Fact]
    public void Validate_WhenPathIsTheWorkspace_RejectsIt()
    {
        var outcome = ToolchainRootPolicy.Validate(Workspace, Workspace, [], Home, static _ => true);

        outcome.Error.ShouldNotBeNull().ShouldContain("already writable");
    }

    [Fact]
    public void Validate_WhenPathIsInsideTheWorkspace_RejectsIt()
    {
        var outcome = ToolchainRootPolicy.Validate(Path.Combine(Workspace, "src"), Workspace, [], Home, static _ => true);

        outcome.Error.ShouldNotBeNull().ShouldContain("already writable");
    }

    [Fact]
    public void Validate_WhenPathIsASiblingWithTheWorkspaceAsPrefix_AcceptsIt()
    {
        var sibling = Workspace + "-tools";

        var outcome = ToolchainRootPolicy.Validate(sibling, Workspace, [], Home, static _ => true);

        outcome.IsAccepted.ShouldBeTrue();
        outcome.Path.ShouldBe(sibling);
    }

    [Fact]
    public void Validate_WhenPathContainsTheWorkspace_RejectsItAsTooBroad()
    {
        var outcome = ToolchainRootPolicy.Validate(Path.Combine(Home, "dev"), Workspace, [], Home, static _ => true);

        outcome.Error.ShouldNotBeNull().ShouldContain("contains the workspace");
    }

    [Fact]
    public void Validate_WhenPathIsAlreadyListedWithDifferentTrailingSeparator_RejectsTheDuplicate()
    {
        var outcome = ToolchainRootPolicy.Validate(
            Homebrew,
            Workspace,
            [Homebrew + Path.DirectorySeparatorChar],
            Home,
            static _ => true);

        outcome.Error.ShouldNotBeNull().ShouldContain("already listed");
    }

    [Fact]
    public void Validate_WhenDirectoryDoesNotExist_RejectsIt()
    {
        var probed = new List<string>();

        var outcome = ToolchainRootPolicy.Validate(Homebrew, Workspace, [], Home, path =>
        {
            probed.Add(path);
            return false;
        });

        outcome.Error.ShouldNotBeNull().ShouldContain("does not exist");
        probed.ShouldBe([Homebrew]);
    }

    [Fact]
    public void Validate_WhenRejectedBeforeTheProbe_NeverTouchesTheFileSystem()
    {
        var probed = false;

        _ = ToolchainRootPolicy.Validate("relative", Workspace, [], Home, _ => probed = true);

        probed.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WhenInputIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ToolchainRootPolicy.Validate(null!, Workspace, [], Home, static _ => true))
            .ParamName.ShouldBe("input");

    [Theory]
    [InlineData("")]
    [InlineData("relative/workspace")]
    public void Validate_WhenWorkspaceIsBlankOrRelative_Throws(string workspace) =>
        Should.Throw<ArgumentException>(() => ToolchainRootPolicy.Validate(Homebrew, workspace, [], Home, static _ => true))
            .ParamName.ShouldBe("workspaceRoot");

    [Fact]
    public void Validate_WhenExistingRootsIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ToolchainRootPolicy.Validate(Homebrew, Workspace, null!, Home, static _ => true))
            .ParamName.ShouldBe("existingRoots");

    [Fact]
    public void Validate_WhenProbeIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => ToolchainRootPolicy.Validate(Homebrew, Workspace, [], Home, null!))
            .ParamName.ShouldBe("directoryExists");
}

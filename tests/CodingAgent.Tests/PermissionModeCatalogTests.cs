// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

public sealed class PermissionModeCatalogTests
{
    [Fact]
    public void All_WhenEnumerated_CoversEveryDefinedModeOnce() =>
        PermissionModeCatalog.All.ShouldBe(Enum.GetValues<PermissionMode>(), ignoreOrder: true);

    [Fact]
    public void MenuLabel_WhenCompared_UsesDistinctAccessKeysAndMatchesTheTitle()
    {
        var keys = new List<char>();

        foreach (var mode in PermissionModeCatalog.All)
        {
            var label = PermissionModeCatalog.MenuLabel(mode);
            var marker = label.IndexOf('&', StringComparison.Ordinal);
            marker.ShouldBeGreaterThanOrEqualTo(0);
            keys.Add(char.ToUpperInvariant(label[marker + 1]));
            label.Replace("&", "", StringComparison.Ordinal).ShouldBe(PermissionModeCatalog.Title(mode));
        }

        keys.Distinct().Count().ShouldBe(keys.Count);
    }

    [Theory]
    [InlineData("ask", (int) PermissionMode.AskForChanges)]
    [InlineData("READONLY", (int) PermissionMode.ReadOnly)]
    [InlineData(" auto ", (int) PermissionMode.AutoApproveWorkspaceEdits)]
    [InlineData("Read-only", (int) PermissionMode.ReadOnly)]
    public void TryParse_WhenArgumentNamesAMode_ReturnsIt(string argument, int expected)
    {
        PermissionModeCatalog.TryParse(argument, out var mode).ShouldBeTrue();
        mode.ShouldBe((PermissionMode) expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("yolo")]
    public void TryParse_WhenArgumentIsNotAMode_ReturnsFalse(string? argument) =>
        PermissionModeCatalog.TryParse(argument, out _).ShouldBeFalse();

    [Fact]
    public void TryParsePaletteId_WhenRoundTripped_ResolvesEveryMode()
    {
        foreach (var mode in PermissionModeCatalog.All)
        {
            PermissionModeCatalog.TryParsePaletteId(PermissionModeCatalog.PaletteId(mode), out var parsed).ShouldBeTrue();
            parsed.ShouldBe(mode);
        }

        PermissionModeCatalog.TryParsePaletteId("info.status", out _).ShouldBeFalse();
    }
}

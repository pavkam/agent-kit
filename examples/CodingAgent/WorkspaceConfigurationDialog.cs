// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Shows workspace confinement and selects the host-declared external read-only roots.</summary>
/// <remarks>Completes with the selected roots, or null when dismissed. Saving starts a fresh session.</remarks>
internal sealed class WorkspaceConfigurationDialog: CodingAgentDialog<ImmutableArray<string>?>
{
    private readonly ImmutableArray<string> _availableRoots;

    /// <summary>Initializes the dialog from fixed host declarations with the current selection loaded.</summary>
    /// <param name="workspaceRoot">The absolute writable workspace root.</param>
    /// <param name="availableRoots">The absolute host-declared roots that may be exposed read-only.</param>
    /// <param name="selectedRoots">The roots selected in the pending runtime configuration.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="workspaceRoot"/> is blank, or an array argument is default.
    /// </exception>
    public WorkspaceConfigurationDialog(
        string workspaceRoot,
        ImmutableArray<string> availableRoots,
        ImmutableArray<string> selectedRoots)
        : base("Configure Workspace", cancelledResult: null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfDefault(availableRoots);
        ArgumentException.ThrowIfDefault(selectedRoots);
        _availableRoots = availableRoots;

        Width = Length.Percent(90);
        MaxWidth = Length.Cells(82);
        Height = Length.Auto;
        MaxHeight = Length.Percent(90);

        RootChoices =
        [
            .. availableRoots.Select(path => new CheckBox(path)
            {
                IsChecked = selectedRoots.Contains(path, StringComparer.Ordinal),
            }),
        ];

        var roots = new Stack
        {
            Orientation = Orientation.Vertical,
            MaxHeight = Length.Cells(8),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Children = { new CheckBox($"Workspace (read/write): {workspaceRoot}") { IsChecked = true, IsEnabled = false } },
        };
        if (RootChoices.IsEmpty)
        {
            roots.Children.Add(new Text("<d>No optional toolchain roots were declared by the host.</d>"));
        }
        else
        {
            foreach (var root in RootChoices)
            {
                roots.Children.Add(root);
            }
        }

        var save = new Button("&Save") { IsDefault = true };
        var cancel = new Button("&Cancel") { IsCancel = true };
        save.Click += (_, _) => Complete(SelectedRoots());
        cancel.Click += (_, _) => Cancel();

        var body = new Stack
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Children =
            {
                new Text("<accent><b>Allowed folders</b></accent>"),
                roots,
                Note("<d>The workspace is required. Selected external folders are mounted read-only for commands.</d>"),
                new Text(""),
                new Text("<accent><b>Sandbox</b></accent>  workspace jail · process isolation · network blocked"),
                Note($"<d>Session store: {Text.Escape(AgentRuntime.SessionDatabasePath(workspaceRoot))}</d>"),
                Note("<d>Saving starts a fresh session. Permissions live in the Permissions menu.</d>"),
            },
        };
        Content = Compose(body, [save, cancel]);
    }

    /// <summary>Gets the selectable external root controls for focused verification.</summary>
    internal ImmutableArray<CheckBox> RootChoices { get; }

    /// <summary>Presents a fresh dialog and completes with the selected roots or null.</summary>
    /// <param name="owner">The attached control whose presentation host shows the dialog.</param>
    /// <param name="workspaceRoot">The absolute writable workspace root.</param>
    /// <param name="availableRoots">The absolute host-declared roots that may be exposed read-only.</param>
    /// <param name="selectedRoots">The roots selected in the pending runtime configuration.</param>
    /// <param name="cancellationToken">Cancels the presentation, completing with null.</param>
    /// <returns>The selected roots, or null when dismissed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is detached or an argument is invalid.</exception>
    public static Task<ImmutableArray<string>?> ShowAsync(
        ControlBase owner,
        string workspaceRoot,
        ImmutableArray<string> availableRoots,
        ImmutableArray<string> selectedRoots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var dialog = new WorkspaceConfigurationDialog(workspaceRoot, availableRoots, selectedRoots);
        return dialog.PresentAsync(owner, dialog.RootChoices.IsEmpty ? null : dialog.RootChoices[0], cancellationToken);
    }

    /// <summary>Creates one explanatory line that wraps to the dialog width instead of clipping.</summary>
    /// <param name="markup">The non-null text markup.</param>
    /// <returns>A wrapping, stretched text control.</returns>
    private static Text Note(string markup) => new(markup)
    {
        Overflow = Overflow.Wrap,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private ImmutableArray<string> SelectedRoots() =>
        [.. _availableRoots.Where((_, index) => RootChoices[index].IsChecked is true)];
}

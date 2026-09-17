// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using SharpVision.Terminal.Input;

/// <summary>Shows workspace confinement and lets the user choose which external folders sandboxed
/// commands may read: host-declared toolchain roots, previously selected roots, and folders typed
/// directly into the dialog.</summary>
/// <remarks>
/// Completes with the selected roots, or null when dismissed. Saving starts a fresh session. A typed
/// folder is validated through <see cref="ToolchainRootPolicy"/> before it joins the list, so the
/// dialog can never hand the runtime a relative, missing, duplicate, or workspace-overlapping root.
/// </remarks>
internal sealed class WorkspaceConfigurationDialog: CodingAgentDialog<ImmutableArray<string>?>
{
    private readonly string _workspaceRoot;
    private readonly Func<string, bool> _directoryExists;
    private readonly List<CheckBox> _rootChoices = [];
    private readonly Stack _rootList;
    private readonly Text _emptyRoots = new("<d>No read-only folders yet. Add one below to expose a toolchain such as Homebrew or the .NET SDK.</d>")
    {
        Overflow = Overflow.Wrap,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
    private readonly Text _validation = new("<d>Absolute path · ~ expands to your home folder · Enter adds</d>")
    {
        Overflow = Overflow.Wrap,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    /// <summary>Initializes the dialog with every known root listed and the current selection loaded.</summary>
    /// <param name="workspaceRoot">The absolute writable workspace root.</param>
    /// <param name="availableRoots">The absolute roots to list; host declarations and previously selected roots.</param>
    /// <param name="selectedRoots">The roots selected in the pending runtime configuration.</param>
    /// <param name="directoryExists">Reports whether one absolute path is an existing directory; null uses the real file system.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="workspaceRoot"/> is blank, or an array argument is default.
    /// </exception>
    public WorkspaceConfigurationDialog(
        string workspaceRoot,
        ImmutableArray<string> availableRoots,
        ImmutableArray<string> selectedRoots,
        Func<string, bool>? directoryExists = null)
        : base("Configure Workspace", cancelledResult: null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfDefault(availableRoots);
        ArgumentException.ThrowIfDefault(selectedRoots);
        _workspaceRoot = workspaceRoot;
        _directoryExists = directoryExists ?? Directory.Exists;

        Width = Length.Percent(90);
        MaxWidth = Length.Cells(84);
        Height = Length.Auto;
        MaxHeight = Length.Percent(92);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var workspace = new Stack
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                new Text($"<b>{Text.Escape(CompactPath(workspaceRoot, home))}</b>") { Overflow = Overflow.Wrap, HorizontalAlignment = HorizontalAlignment.Stretch },
                Note($"<d>Sessions  {Text.Escape(CompactPath(AgentRuntime.SessionDatabasePath(workspaceRoot), home))}</d>"),
            },
        };

        _rootList = new Stack
        {
            Orientation = Orientation.Vertical,
            MaxHeight = Length.Cells(6),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        foreach (var root in availableRoots.Concat(selectedRoots).Distinct(StringComparer.Ordinal))
        {
            AddChoice(root, selectedRoots.Contains(root, StringComparer.Ordinal));
        }

        PathInput = new TextInput
        {
            Placeholder = "/absolute/path/to/toolchain",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            StartAffix = new Affix("+", "+"),
        };
        PathInput.KeyDown += OnPathInputKeyDown;
        PathInput.TextChanged += (_, _) => ResetValidationHint();
        AddButton = new Button("&Add");
        AddButton.Click += (_, _) => TryAddRoot();

        var addRow = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        addRow.Columns.Add(Track.Star(1));
        addRow.Columns.Add(Track.Auto());
        Grid.SetColumn(AddButton, 1);
        AddButton.Margin = new Thickness(1, 0, 0, 0);
        addRow.Children.Add(PathInput);
        addRow.Children.Add(AddButton);
        // Themes with a button shadow paint it one row below the button; keep that row clear so the
        // shadow never lands on the validation line.
        _validation.Margin = new Thickness(0, 1, 0, 0);

        var roots = new Stack
        {
            Orientation = Orientation.Vertical,
            Children = { _emptyRoots, _rootList, addRow, _validation },
        };
        RefreshEmptyState();

        var sandbox = new Stack
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                Note("<success>✓</success> Workspace jail   <success>✓</success> Isolated processes   <success>✓</success> No network"),
                Note("<d>Edit and command approvals live in the Permissions menu. Saving here starts a fresh session.</d>"),
            },
        };

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
                Section("Workspace · read/write", workspace),
                Section("Read-only folders for commands", roots),
                Section("Sandbox", sandbox),
            },
        };
        Content = Compose(body, [save, cancel], hint: "Space toggles · Enter adds a folder");
    }

    /// <summary>Gets the selectable external root controls, in list order, for focused verification.</summary>
    internal IReadOnlyList<CheckBox> RootChoices => _rootChoices;

    /// <summary>Gets the folder entry used to add a new read-only root.</summary>
    internal TextInput PathInput { get; }

    /// <summary>Gets the button that validates and adds the typed folder.</summary>
    internal Button AddButton { get; }

    /// <summary>Gets the current inline validation text shown beneath the folder entry.</summary>
    internal string ValidationText => _validation.Content;

    /// <summary>Presents a fresh dialog and completes with the selected roots or null.</summary>
    /// <param name="owner">The attached control whose presentation host shows the dialog.</param>
    /// <param name="workspaceRoot">The absolute writable workspace root.</param>
    /// <param name="availableRoots">The absolute roots to list; host declarations and previously selected roots.</param>
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
        ControlBase initialFocus = dialog._rootChoices.Count == 0 ? dialog.PathInput : dialog._rootChoices[0];
        return dialog.PresentAsync(owner, initialFocus, cancellationToken);
    }

    /// <summary>Validates the typed folder and, when accepted, appends it to the list already checked.</summary>
    /// <returns><see langword="true"/> when a folder was added; otherwise the validation line explains why not.</returns>
    internal bool TryAddRoot()
    {
        var outcome = ToolchainRootPolicy.Validate(
            PathInput.Text,
            _workspaceRoot,
            _rootChoices.Select(static choice => choice.Text),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            _directoryExists);
        if (!outcome.IsAccepted)
        {
            _validation.Content = $"<error>{Text.Escape(outcome.Error!)}</error>";
            return false;
        }

        AddChoice(outcome.Path!, isChecked: true);
        RefreshEmptyState();
        PathInput.Text = string.Empty;
        _validation.Content = $"<success>Added</success> <d>{Text.Escape(outcome.Path!)}</d>";
        return true;
    }

    /// <summary>Abbreviates a path for display: a leading home-directory prefix becomes <c>~</c> and
    /// any long hexadecimal segment, such as a workspace hash, keeps only its first eight digits.</summary>
    /// <param name="path">The non-null absolute path to display.</param>
    /// <param name="homeDirectory">The absolute home directory, or null/blank to skip the <c>~</c> substitution.</param>
    /// <returns>The abbreviated display path; never used as a file-system path.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    internal static string CompactPath(string path, string? homeDirectory)
    {
        ArgumentNullException.ThrowIfNull(path);
        var home = homeDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var compact = string.IsNullOrEmpty(home)
            ? path
            : string.Equals(path, home, StringComparison.Ordinal)
                ? "~"
                : path.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    ? "~" + path[home.Length..]
                    : path;
        var segments = compact.Split(Path.DirectorySeparatorChar);
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].Length > 24 && segments[index].All(Uri.IsHexDigit))
            {
                segments[index] = string.Concat(segments[index].AsSpan(0, 8), "…");
            }
        }

        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    private void AddChoice(string root, bool isChecked)
    {
        var choice = new CheckBox(root) { IsChecked = isChecked, HorizontalAlignment = HorizontalAlignment.Stretch };
        _rootChoices.Add(choice);
        _rootList.Children.Add(choice);
    }

    private void RefreshEmptyState()
    {
        var empty = _rootChoices.Count == 0;
        _emptyRoots.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        _rootList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ResetValidationHint()
    {
        if (_validation.Content.StartsWith("<error>", StringComparison.Ordinal))
        {
            _validation.Content = "<d>Absolute path · ~ expands to your home folder · Enter adds</d>";
        }
    }

    private void OnPathInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Stroke.Code == Code.Enter && (e.Stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock)) == Modifiers.None)
        {
            // Enter inside the folder entry adds the folder; it must not reach the window, where
            // it would activate the default Save button and close the dialog mid-edit.
            _ = TryAddRoot();
            e.IsHandled = true;
        }
    }

    private ImmutableArray<string> SelectedRoots() =>
        [.. _rootChoices.Where(static choice => choice.IsChecked is true).Select(static choice => choice.Text)];
}

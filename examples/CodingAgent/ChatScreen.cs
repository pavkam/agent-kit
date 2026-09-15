// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using System.Diagnostics;
using System.Globalization;
using System.Text;

using SharpVision.Controls.Document;
using SharpVision.Controls.SyntaxHighlighting;
using SharpVision.Documents.Markdown;
using SharpVision.Scrolling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;

/// <summary>The coding agent's chat screen: a menu bar, a scrollable message list, a status bar, and a
/// command-palette-driven prompt.</summary>
internal sealed class ChatScreen: Screen, IApprovalPrompt, IHumanQuestionPrompt, IConversationEventObserver
{
    private readonly List<ChatEntry> _entries = [];
    private readonly Stack _transcript = CreateTranscript();

    private readonly TextInput _prompt = CreateComposer();
    private readonly CommandPalette _commandPalette = CreateCommandPalette();
    private readonly Spinner _spinner = new() { Visibility = Visibility.Collapsed };
    private readonly Text _status = new("Ready");
    private readonly Text _permissionStatus = new();
    private readonly Text _usageStatus = new("<d>usage –</d>");
    private readonly Text _modelStatus = new();
    private readonly Text _sidebarContext = new("<d>No usage reported yet.</d>");
    private readonly Stack _sidebarTodo = new() { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly MarkdownDocumentReader _markdownReader = new();
    private readonly SessionUsage _usage = new();
    private readonly PermissionModeController _permissions = new();
    private readonly Dictionary<string, int> _liveToolRows = [];
    private readonly HashSet<ControlBase> _selectedTranscriptRows = [];
    private readonly string _workspaceRoot;
    private readonly Dock _sidebar;
    private MenuItem? _newSessionMenuItem;
    private MenuItem? _recentSessionsMenuItem;
    private MenuItem? _clearTranscriptMenuItem;
    private MenuItem? _stopTurnMenuItem;
    private MenuItem? _modelMenuItem;
    private MenuItem? _reasoningMenuItem;
    private ImmutableArray<MenuItem> _modelMenuItems = [];
    private ImmutableArray<MenuItem> _reasoningMenuItems = [];
    private ImmutableArray<MenuItem> _permissionMenuItems = [];

    private readonly List<string> _history = [];

    private Application? _application;
    private OwnedConversationSession? _conversation;
    private CancellationTokenSource? _turnCancellation;
    private TaskCompletionSource<bool>? _pendingApproval;
    private TaskCompletionSource<HumanQuestionSelection>? _pendingQuestion;
    private HumanQuestionPrompt? _pendingQuestionPrompt;
    private string _questionDraft = "";
    private string? _pendingApprovalToolKey;
    private ImmutableArray<TodoItem> _todoItems = [];
    private string? _todoTitle;
    private bool _busy;
    private bool _followLatest = true;
    private bool _transcriptRefreshPending;
    private bool _reasoningHasContent;
    private bool _turnCancellationRequested;
    private bool _turnCancellationRendered;
    private int? _assistantRow;
    private int? _reasoningRow;
    private int _historyIndex;
    private string _historyDraft = "";
    private CodingAgentConfiguration _configuration;

    public ChatScreen(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
        _configuration = CodingAgentConfiguration.CreateDefault();
        RefreshStatusDetails();
        AppendEntry(new ChatEntry(ChatEntryKind.System, "Workspace", Text.Escape(workspaceRoot)));
        _commandPalette.Resolver = ResolvePaletteCommandsAsync;
        _commandPalette.ItemTemplate = new ItemTemplate(BuildCommandPaletteRow);
        _commandPalette.ItemInvoked += OnCommandPaletteItemInvoked;
        _ = _commandPalette.AddHandler(Events.Key, OnCommandPaletteKey, handledEventsToo: true);

        var menuBar = BuildMenuBar();
        // Reachable by Alt+mnemonic without joining ordinary Tab traversal or initial focus - the
        // prompt below must always keep focus by default, exactly like SharpVision's own
        // TextEditor sample (EditorScreen.cs) does for its menu bar. The one-cell inset is the
        // menu's own padding rather than a wrapping container, so the whole strip - inset cells
        // included - sits on the theme's Bar plane instead of showing the desktop color at its
        // edges, and the strip itself is the visual boundary the way a Turbo Vision menu bar is.
        menuBar.IsTabStop = false;
        menuBar.Padding = new Thickness(1, 0);
        menuBar.HorizontalAlignment = HorizontalAlignment.Stretch;

        var statusBar = new StatusBar { Padding = new Thickness(1, 0) };
        statusBar.Items.Add(new StatusBarItem
        {
            Content = new Stack
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new Stack
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 1,
                        Children = { _spinner, _status },
                    },
                    CreateStatusSegment(_permissionStatus),
                    CreateStatusSegment(_usageStatus),
                    CreateStatusSegment(_modelStatus),
                    CreateStatusSegment(new Text($"<d>{Text.Escape(WorkspaceLabel(workspaceRoot))}</d>")),
                },
            },
        });

        var promptRow = new Dock { HorizontalAlignment = HorizontalAlignment.Stretch };
        promptRow.Children.Add(_prompt);

        _sidebar = BuildSidebar();

        var layout = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Dock.SetSide(menuBar, DockSide.Top);
        Dock.SetSide(statusBar, DockSide.Bottom);
        Dock.SetSide(promptRow, DockSide.Bottom);
        Dock.SetSide(_sidebar, DockSide.Right);
        layout.Children.Add(menuBar);
        layout.Children.Add(statusBar);
        layout.Children.Add(promptRow);
        layout.Children.Add(_sidebar);
        layout.Children.Add(_transcript);
        layout.BoundsChanged += (_, _) =>
        {
            RefreshResponsiveLayout(layout.Bounds.Width);
            UpdateComposerMaximumRows(_prompt, layout.Bounds.Width);
        };
        _transcript.BoundsChanged += (_, _) => ScrollToTailAfterLayout();
        _transcript.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(_transcript.Extent) or nameof(_transcript.Viewport))
            {
                ScrollToTailAfterLayout();
            }
        };

        var root = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        root.Children.Add(layout);
        Overlay.SetTop(_commandPalette, Length.Cells(2));
        root.Children.Add(_commandPalette);

        _transcript.KeyDown += OnTranscriptKeyDown;
        _prompt.TextChanged += OnPromptTextChanged;
        _prompt.KeyDown += OnPromptKeyDown;
        _ = _prompt.AddHandler(Events.Key, OnPromptPreviewKey, handledEventsToo: true);
        _ = root.AddHandler(Events.Key, OnRootPreviewKey, handledEventsToo: true);

        InitializeContent(root);
    }

    /// <summary>Creates the retained scrolling host that leaves pointer selection to each semantic row.</summary>
    /// <returns>A nonselecting, nonfocusable vertical scroll container.</returns>
    internal static Stack CreateTranscript() => new()
    {
        Orientation = Orientation.Vertical,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        AutoScroll = true,
        ScrollBars = ScrollBars.Vertical,
        ShowScrollBars = ShowScrollBars.WhenNeeded,
        IsTextSelectionEnabled = false,
        IsFocusable = false,
        IsTabStop = false,
    };

    /// <summary>Creates the multiline composer with intrinsic vertical growth and bounded overflow scrolling.</summary>
    /// <returns>A one-row-empty composer whose measured height follows explicit and visually wrapped lines.</returns>
    internal static TextInput CreateComposer() => new()
    {
        Placeholder = "Ask the coding agent, or press / for commands... (Enter sends · Shift+Enter adds a line)",
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Height = Length.Auto,
        MinHeight = Length.Cells(3),
        MaxHeight = Length.Cells(7),
        AcceptsReturn = true,
        WordWrap = true,
        ScrollBars = ScrollBars.Vertical,
        ShowScrollBars = ShowScrollBars.WhenNeeded,
    };

    /// <summary>Applies the terminal-width-relative content-row ceiling to one composer.</summary>
    /// <param name="composer">The non-null multiline composer to update.</param>
    /// <param name="terminalWidth">The positive current terminal width in cells.</param>
    /// <exception cref="ArgumentNullException"><paramref name="composer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="terminalWidth"/> is not positive.</exception>
    internal static void UpdateComposerMaximumRows(TextInput composer, int terminalWidth)
    {
        ArgumentNullException.ThrowIfNull(composer);
        composer.MaxHeight = Length.Cells(ComposerMaximumRows(terminalWidth) + 2);
    }

    /// <summary>Calculates the inclusive content-row ceiling from the current terminal width.</summary>
    /// <param name="terminalWidth">The positive current terminal width in cells.</param>
    /// <returns>Between one and five visible editor rows.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="terminalWidth"/> is not positive.</exception>
    internal static int ComposerMaximumRows(int terminalWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(terminalWidth);
        return Math.Max(1, Math.Min(5, (int) Math.Floor(terminalWidth * 0.10)));
    }

    protected override void OnStarted(Application application)
    {
        _application = application;
        _ = application.Focus.Focus(_prompt);
    }

    private Menu BuildMenuBar()
    {
        var menu = MenuBuilder.Horizontal(spacing: 0)
            .Submenu(
                "&Session",
                session => session
                    .Item("&New session", onInvoke: () => InvokeMenu(() => ExecuteSlashCommand("/new")), isEnabled: !_busy)
                    .Item("&Recent sessions", onInvoke: () => InvokeMenu(() => ExecuteSlashCommand("/sessions")), isEnabled: !_busy)
                    .Item("&Clear transcript", onInvoke: () => InvokeMenu(() => ExecuteSlashCommand("/clear")))
                    .Separator()
                    .Item("Configure &workspace…", onInvoke: () => InvokeMenu(ShowWorkspaceConfiguration, restorePromptFocus: false))
                    .Separator()
                    .Item("&Quit", shortcut: "Ctrl+Q", onInvoke: () => _application?.Shutdown()))
            .Submenu(
                "&View",
                view => view
                    .Item("&Follow latest", shortcut: "Ctrl+End", onInvoke: () => InvokeMenu(FollowLatest))
                    .Item("&Previous message", shortcut: "Page Up", onInvoke: () => InvokeMenu(() => ScrollTranscript(-1)))
                    .Item("&Next message", shortcut: "Page Down", onInvoke: () => InvokeMenu(() => ScrollTranscript(1))))
            .Submenu(
                "&Agent",
                agent => agent
                    .Item("Command &palette", shortcut: "Ctrl+K", onInvoke: () => InvokeMenu(OpenCommandPalette, restorePromptFocus: false))
                    .Separator()
                    .Item("&Stop current turn", shortcut: "Esc", onInvoke: () => InvokeMenu(StopTurn))
                    .Separator()
                    .Submenu(
                        ModelMenuLabel(_configuration),
                        model => model
                            .Radio("&Terra · gpt-5.6-terra", "agent-model",
                                isChecked: IsCurrentModel("gpt-5.6-terra"),
                                onInvoke: () => InvokeMenu(() => SetModel("gpt-5.6-terra")))
                            .Radio("&Sol · gpt-5.6-sol", "agent-model",
                                isChecked: IsCurrentModel("gpt-5.6-sol"),
                                onInvoke: () => InvokeMenu(() => SetModel("gpt-5.6-sol")))
                            .Radio("&Astra · gpt-6-astra", "agent-model",
                                isChecked: IsCurrentModel("gpt-6-astra"),
                                onInvoke: () => InvokeMenu(() => SetModel("gpt-6-astra"))))
                    .Submenu(
                        ReasoningMenuLabel(_configuration.ReasoningEffort),
                        reasoning => reasoning
                            .Radio("&Off", "agent-reasoning",
                                isChecked: _configuration.ReasoningEffort == LlmReasoningEffort.None,
                                onInvoke: () => InvokeMenu(() => SetReasoningEffort(LlmReasoningEffort.None)))
                            .Radio("&Low", "agent-reasoning",
                                isChecked: _configuration.ReasoningEffort == LlmReasoningEffort.Low,
                                onInvoke: () => InvokeMenu(() => SetReasoningEffort(LlmReasoningEffort.Low)))
                            .Radio("&Medium", "agent-reasoning",
                                isChecked: _configuration.ReasoningEffort == LlmReasoningEffort.Medium,
                                onInvoke: () => InvokeMenu(() => SetReasoningEffort(LlmReasoningEffort.Medium)))
                            .Radio("&High", "agent-reasoning",
                                isChecked: _configuration.ReasoningEffort == LlmReasoningEffort.High,
                                onInvoke: () => InvokeMenu(() => SetReasoningEffort(LlmReasoningEffort.High)))
                            .Radio("E&xtra high", "agent-reasoning",
                                isChecked: _configuration.ReasoningEffort == LlmReasoningEffort.ExtraHigh,
                                onInvoke: () => InvokeMenu(() => SetReasoningEffort(LlmReasoningEffort.ExtraHigh))))
                    .Separator()
                    .Item("Agent &configuration…", onInvoke: () => InvokeMenu(ShowAgentConfiguration, restorePromptFocus: false))
                    .Item("Available &tools", onInvoke: () => InvokeMenu(ShowToolReference, restorePromptFocus: false)))
            .Submenu("&Permissions", permissions =>
            {
                foreach (var mode in PermissionModeCatalog.All)
                {
                    _ = permissions.Radio(
                        PermissionModeCatalog.MenuLabel(mode),
                        "permission-mode",
                        isChecked: mode == _permissions.Mode,
                        onInvoke: () => InvokeMenu(() => SetPermissionMode(mode)));
                }
            })
            .Submenu(
                "&Help",
                help => help
                    .Item("&Commands", onInvoke: () => InvokeMenu(ShowCommandReference, restorePromptFocus: false))
                    .Item("&Keyboard shortcuts", onInvoke: () => InvokeMenu(ShowKeyboardShortcuts, restorePromptFocus: false)))
            .Build();

        // Items the screen toggles later are found by their authored label rather than by
        // position, so rearranging a menu cannot silently retarget RefreshMenuState.
        var sessionMenu = FindMenuItem(menu, "&Session").Submenu!;
        _newSessionMenuItem = FindMenuItem(sessionMenu, "&New session");
        _recentSessionsMenuItem = FindMenuItem(sessionMenu, "&Recent sessions");
        _clearTranscriptMenuItem = FindMenuItem(sessionMenu, "&Clear transcript");
        FindMenuItem(sessionMenu, "&Quit").Shortcut = new KeyGesture(Code.Character, Modifiers.Control, new Rune('q'));
        var agentMenu = FindMenuItem(menu, "&Agent").Submenu!;
        _stopTurnMenuItem = FindMenuItem(agentMenu, "&Stop current turn");
        _modelMenuItem = FindMenuItem(agentMenu, ModelMenuLabel(_configuration));
        _reasoningMenuItem = FindMenuItem(agentMenu, ReasoningMenuLabel(_configuration.ReasoningEffort));
        _modelMenuItems = [.. _modelMenuItem.Submenu!.Items.Cast<MenuItem>()];
        _reasoningMenuItems = [.. _reasoningMenuItem.Submenu!.Items.Cast<MenuItem>()];
        _permissionMenuItems = [.. FindMenuItem(menu, "&Permissions").Submenu!.Items.Cast<MenuItem>()];
        RefreshMenuState();
        return menu;
    }

    /// <summary>Finds the one owned item whose authored label matches exactly.</summary>
    /// <param name="menu">The non-null menu to search.</param>
    /// <param name="text">The exact authored label, mnemonic marker included.</param>
    /// <returns>The matching item.</returns>
    /// <exception cref="InvalidOperationException">No item, or more than one item, carries the label.</exception>
    private static MenuItem FindMenuItem(Menu menu, string text) =>
        menu.Items.OfType<MenuItem>().Single(item => string.Equals(item.Text, text, StringComparison.Ordinal));

    /// <summary>Builds the live model submenu label from a validated configuration.</summary>
    /// <param name="configuration">The non-null configuration whose selected model should be shown.</param>
    /// <returns>A mnemonic-bearing label containing the short model name or exact unknown identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    internal static string ModelMenuLabel(CodingAgentConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var model = CodingAgentConfiguration.Models.FirstOrDefault(option =>
            string.Equals(option.ModelId, configuration.ModelId, StringComparison.Ordinal));
        return $"&Model · {model?.Name ?? configuration.ModelId}";
    }

    /// <summary>Builds the compact model-and-effort value shown in the status bar.</summary>
    /// <param name="configuration">The non-null configuration whose active choices should be shown.</param>
    /// <returns>The short model name and readable reasoning effort.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    internal static string ModelStatusLabel(CodingAgentConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var model = CodingAgentConfiguration.Models.FirstOrDefault(option =>
            string.Equals(option.ModelId, configuration.ModelId, StringComparison.Ordinal));
        return $"{model?.Name ?? configuration.ModelId} · {ReasoningEffortName(configuration.ReasoningEffort)}";
    }

    /// <summary>Wraps one status value with the application's exact inter-item separator.</summary>
    /// <param name="content">The non-null status value to place after the separator.</param>
    /// <returns>A horizontal segment beginning with a literal space-pipe-space separator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    internal static Stack CreateStatusSegment(ControlBase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new Stack
        {
            Orientation = Orientation.Horizontal,
            Children = { new Text(" | "), content },
        };
    }

    /// <summary>Builds the live reasoning-effort submenu label.</summary>
    /// <param name="effort">The defined portable reasoning effort.</param>
    /// <returns>A mnemonic-bearing label containing concise effort text.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="effort"/> is unknown.</exception>
    internal static string ReasoningMenuLabel(LlmReasoningEffort effort) =>
        $"&Effort · {ReasoningEffortName(effort)}";

    private static string ReasoningEffortName(LlmReasoningEffort effort) => effort switch
    {
        LlmReasoningEffort.Low => "Low",
        LlmReasoningEffort.Medium => "Medium",
        LlmReasoningEffort.High => "High",
        LlmReasoningEffort.ExtraHigh => "Extra high",
        LlmReasoningEffort.None => "Off",
        _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "The reasoning effort is unknown."),
    };

    private bool IsCurrentModel(string modelId) =>
        string.Equals(_configuration.ModelId, modelId, StringComparison.Ordinal);

    private void InvokeMenu(Action action, bool restorePromptFocus = true)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher?.Post(() =>
        {
            action();
            if (restorePromptFocus && _application is not null)
            {
                _ = _application.Focus.Focus(_prompt);
            }
        });
    }

    private Dock BuildSidebar()
    {
        RefreshSidebarTodo();
        var contextHeader = new Text("<accent><b>Context</b></accent>");
        var todoHeader = new Text("<accent><b>Todo</b></accent>");
        var content = new Stack
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Spacing = 1,
            Padding = new Thickness(1, 0),
            Children = { contextHeader, _sidebarContext, todoHeader, _sidebarTodo },
        };

        return new Dock
        {
            Width = Length.Cells(32),
            VerticalAlignment = VerticalAlignment.Stretch,
            Border = new Border(
                BorderSide.Left,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Children = { content },
        };
    }

    /// <summary>Creates the themed, responsive application command palette.</summary>
    /// <returns>A hidden palette whose native resolver popup inherits the active SharpVision theme.</returns>
    internal static CommandPalette CreateCommandPalette() => new()
    {
        Width = Length.Percent(88),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Top,
        Visibility = Visibility.Collapsed,
        Placeholder = "Search sessions, agent actions, permissions, and help…",
        StartAffix = new Affix("⌘", ">"),
        EndAffix = new Affix("×", "x"),
        DropDownHeight = Length.Percent(58),
        RowHeight = Length.Auto,
        PopupChrome = new PopupChrome
        {
            Border = new Border(
                BorderSide.All,
                BorderGlyphStyle.Paired,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Shadow = new Shadow(
                true,
                ShadowMode.BlockGlyph,
                new Point(1, 1),
                new Rune('▓'),
                SemanticColor.ControlShadow,
                SemanticColor.ControlShadow,
                SemanticDecoration.Shadow),
        },
    };

    private static Stack BuildCommandPaletteRow(object? item)
    {
        var command = (CommandPaletteItem) item!;
        var shortcut = new Text(command.Shortcut is { Length: > 0 }
            ? $"<reverse> {Text.Escape(command.Shortcut)} </reverse>"
            : string.Empty)
        {
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Dock.SetSide(shortcut, DockSide.Right);
        var title = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                shortcut,
                new Text($"<accent><b>{Text.Escape(command.Title)}</b></accent>"),
            },
        };
        var badge = command.Badge is { Length: > 0 }
            ? $" · <success>{Text.Escape(command.Badge)}</success>"
            : string.Empty;
        return new Stack
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(1, 0),
            Children =
            {
                title,
                new Text($"<d>{Text.Escape(command.Group.ToUpperInvariant())}{badge} · {Text.Escape(command.Description)}</d>"),
            },
        };
    }

    private void OnPromptTextChanged(object? sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_prompt.Text == "/" && !_busy && _pendingApproval is null && _pendingQuestion is null)
        {
            _prompt.Text = string.Empty;
            OpenCommandPalette();
        }
    }

    private void OpenCommandPalette()
    {
        if (_pendingApproval is not null || _pendingQuestion is not null)
        {
            _status.Content = "Finish the active prompt before opening commands";
            return;
        }

        _commandPalette.Text = string.Empty;
        _commandPalette.Visibility = Visibility.Visible;
        _commandPalette.Refresh();
        _ = _commandPalette.Open();
    }

    private void CloseCommandPalette(bool restorePromptFocus)
    {
        _commandPalette.Close();
        _commandPalette.Visibility = Visibility.Collapsed;
        _commandPalette.Text = string.Empty;
        if (restorePromptFocus && _application is not null)
        {
            _ = _application.Focus.Focus(_prompt);
        }
    }

    private void OnCommandPaletteKey(object? sender, KeyEventArgs e)
    {
        _ = sender;
        if (e.Phase == RoutingPhase.Bubble && e.Stroke.Code == Code.Escape)
        {
            Dispatcher?.Post(() => CloseCommandPalette(restorePromptFocus: true));
        }
    }

    private void OnRootPreviewKey(object? sender, KeyEventArgs e)
    {
        _ = sender;
        if (e.Phase == RoutingPhase.Preview && IsCommandPaletteChord(e))
        {
            OpenCommandPalette();
            e.IsHandled = true;
        }
    }

    private ValueTask<IReadOnlyList<object?>> ResolvePaletteCommandsAsync(
        string searchTerms,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var matches = MatchCommandPaletteItems(BuildCommandPaletteItems(_busy, _permissions.Mode), searchTerms);
        return ValueTask.FromResult<IReadOnlyList<object?>>([.. matches.Cast<object?>()]);
    }

    /// <summary>Builds the complete palette catalog with current run and permission state.</summary>
    /// <param name="busy">Whether an agent turn is active.</param>
    /// <param name="permissionMode">The current permission mode shown as exact state evidence.</param>
    /// <returns>The deterministic application command catalog.</returns>
    internal static ImmutableArray<CommandPaletteItem> BuildCommandPaletteItems(
        bool busy,
        PermissionMode permissionMode) =>
    [
        new("session.new", "Session", "New session", "Start with a clean conversation.", keywords: "fresh reset chat"),
        new("session.recent", "Session", "Recent sessions", "Browse durable conversations from this workspace.", keywords: "history list open"),
        new("session.resume", "Session", "Resume session by ID…", "Prepare the composer to open an exact durable session.", keywords: "continue restore conversation"),
        new("transcript.clear", "Session", "Clear transcript", "Clear visible messages while keeping the current session.", keywords: "clean view"),
        new("view.follow", "View", "Follow latest message", "Jump to the tail and keep new output visible.", "Ctrl+End", "bottom scroll"),
        new("view.previous", "View", "Previous transcript page", "Move one viewport toward older messages.", "Page Up", "scroll older"),
        new("view.next", "View", "Next transcript page", "Move one viewport toward newer messages.", "Page Down", "scroll newer"),
        new("agent.stop", "Agent", "Stop current turn", busy ? "Cancel the active agent turn." : "Report that no agent turn is active.", "Esc", "cancel abort", busy ? "RUNNING" : null),
        .. PermissionModeCatalog.All.Select(mode => new CommandPaletteItem(
            PermissionModeCatalog.PaletteId(mode),
            "Permissions",
            PermissionModeCatalog.Title(mode),
            PermissionModeCatalog.Description(mode),
            keywords: "permission security approval safe",
            badge: mode == permissionMode ? "CURRENT" : null)),
        new("info.status", "Agent", "Agent configuration", "Configure model, reasoning effort, and turn limit; saving starts a fresh session.", keywords: "health info settings model llm provider"),
        new("info.tools", "Agent", "Available tools", "Inspect model-facing tools and the active permission policy.", keywords: "capabilities functions sandbox"),
        new("info.workspace", "Agent", "Configure workspace", "Select the read-only toolchain folders exposed beside the workspace.", keywords: "folder repository path roots sandbox"),
        new("help.shortcuts", "Help", "Keyboard shortcuts", "Show composer, history, transcript, and application keys.", keywords: "keys commands"),
        new("help.commands", "Help", "Slash command reference", "List the textual commands accepted by the composer.", keywords: "help list"),
        new("app.quit", "Application", "Quit CodingAgent", "Close the terminal application.", "Ctrl+Q", "exit close"),
    ];

    /// <summary>Filters the catalog by case-insensitive words across visible copy and keywords.</summary>
    /// <param name="items">The complete immutable catalog.</param>
    /// <param name="searchTerms">The freely entered palette query.</param>
    /// <returns>Catalog-order matches containing every nonempty query word.</returns>
    internal static ImmutableArray<CommandPaletteItem> MatchCommandPaletteItems(
        ImmutableArray<CommandPaletteItem> items,
        string searchTerms)
    {
        ArgumentException.ThrowIfDefault(items);
        ArgumentNullException.ThrowIfNull(searchTerms);
        var words = searchTerms.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return
        [
            .. items
                .Select(static (item, index) => (Item: item, Index: index))
                .Where(candidate => words.All(word => candidate.Item.SearchText.Contains(word, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(candidate => string.Equals(candidate.Item.Title, searchTerms, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(candidate => candidate.Item.Title.StartsWith(searchTerms, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(candidate => candidate.Item.Title.EndsWith(searchTerms, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(candidate => words.All(word => candidate.Item.Title.Contains(word, StringComparison.OrdinalIgnoreCase)))
                .ThenBy(static candidate => candidate.Index)
                .Select(static candidate => candidate.Item),
        ];
    }

    private void OnCommandPaletteItemInvoked(object? sender, ItemInvokedEventArgs e)
    {
        _ = sender;
        var command = (CommandPaletteItem) e.Item!;
        CloseCommandPalette(restorePromptFocus: false);
        switch (command.Id)
        {
            case "session.new": ExecuteSlashCommand("/new"); break;
            case "session.recent": ExecuteSlashCommand("/sessions"); break;
            case "session.resume": StagePrompt("/resume "); break;
            case "transcript.clear": ExecuteSlashCommand("/clear"); break;
            case "view.follow": FollowLatest(); break;
            case "view.previous": ScrollTranscript(-1); break;
            case "view.next": ScrollTranscript(1); break;
            case "agent.stop": StopTurn(); break;
            case "info.status": ShowAgentConfiguration(); return;
            case "info.tools": ShowToolReference(); return;
            case "info.workspace": ShowWorkspaceConfiguration(); return;
            case "help.shortcuts": ShowKeyboardShortcuts(); return;
            case "help.commands": ShowCommandReference(); return;
            case "app.quit": _application?.Shutdown(); return;
            default:
                if (!PermissionModeCatalog.TryParsePaletteId(command.Id, out var mode))
                {
                    throw new UnreachableException($"Unknown command palette item '{command.Id}'.");
                }

                SetPermissionMode(mode);
                break;
        }

        Dispatcher?.Post(() =>
        {
            if (_application is not null)
            {
                _ = _application.Focus.Focus(_prompt);
            }
        });
    }

    private void StagePrompt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _prompt.Text = text;
        _prompt.CaretIndex = text.Length;
    }

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Stroke.Code == Code.PageUp)
        {
            ScrollTranscript(-1);
            e.IsHandled = true;
            return;
        }

        if (e.Stroke.Code == Code.PageDown)
        {
            ScrollTranscript(1);
            e.IsHandled = true;
            return;
        }

        if (_pendingApproval is { } approval)
        {
            if (e.Stroke.Code == Code.Enter || e.Stroke.Character?.Value == 'y' || e.Stroke.Character?.Value == 'Y')
            {
                _ = approval.TrySetResult(true);
                e.IsHandled = true;
            }
            else if (e.Stroke.Code == Code.Escape || e.Stroke.Character?.Value == 'n' || e.Stroke.Character?.Value == 'N')
            {
                _ = approval.TrySetResult(false);
                e.IsHandled = true;
            }

            return;
        }

        if (_pendingQuestion is not null)
        {
            if (e.Stroke.Code == Code.Escape)
            {
                _turnCancellation?.Cancel();
                e.IsHandled = true;
            }

            return;
        }

        if (_busy)
        {
            if (e.Stroke.Code == Code.Escape)
            {
                _turnCancellation?.Cancel();
                e.IsHandled = true;
            }

            return;
        }

        if (IsControlChord(e, 'p'))
        {
            RecallHistory(-1);
            e.IsHandled = true;
        }
        else if (IsControlChord(e, 'n'))
        {
            RecallHistory(1);
            e.IsHandled = true;
        }
    }

    private void OnPromptPreviewKey(object? sender, KeyEventArgs e)
    {
        _ = sender;
        if (e.Phase != RoutingPhase.Preview)
        {
            return;
        }

        if (e.Stroke.Code == Code.PageUp)
        {
            ScrollTranscript(-1);
            e.IsHandled = true;
            return;
        }

        if (e.Stroke.Code == Code.PageDown)
        {
            ScrollTranscript(1);
            e.IsHandled = true;
            return;
        }

        if (e.Stroke.Code == Code.End
            && (e.Stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock)) == Modifiers.Control)
        {
            FollowLatest();
            e.IsHandled = true;
            return;
        }

        if (e.IsHandled)
        {
            return;
        }

        if (_pendingApproval is { } approval && e.Stroke.Code == Code.Enter)
        {
            _ = approval.TrySetResult(true);
            e.IsHandled = true;
            return;
        }

        if (_pendingQuestion is not null
            && IsComposerSubmitKey(e.Stroke.Code, e.Stroke.Modifiers))
        {
            TrySubmitQuestionAnswer();
            e.IsHandled = true;
            return;
        }

        if (_busy || _pendingApproval is not null || _pendingQuestion is not null ||
            !IsComposerSubmitKey(e.Stroke.Code, e.Stroke.Modifiers))
        {
            return;
        }

        e.IsHandled = true;
        _ = SubmitPromptAsync();
    }

    /// <summary>Determines whether one key stroke submits the current composer value.</summary>
    /// <param name="code">The logical key code.</param>
    /// <param name="modifiers">The active modifiers, including any lock-state flags.</param>
    /// <returns><see langword="true"/> for an unmodified Enter press; <see langword="false"/> when Shift requests a line break or another command modifier is active.</returns>
    internal static bool IsComposerSubmitKey(Code code, Modifiers modifiers) =>
        code == Code.Enter &&
        (modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock)) == Modifiers.None;

    private void OnTranscriptKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Stroke.Code == Code.Escape && _application is not null)
        {
            ClearTranscriptSelections();
            _ = _application.Focus.Focus(_prompt);
            e.IsHandled = true;
        }
    }

    private void ClearTranscriptSelections()
    {
        ClearTranscriptSelections(_transcript.Children
            .OfType<Stack>()
            .SelectMany(static row => row.Children.OfType<Document>()));
    }

    /// <summary>Clears a stable snapshot of selectable transcript documents.</summary>
    /// <param name="documents">The non-null document sequence to snapshot before selection callbacks may rebuild its source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> is null.</exception>
    internal static void ClearTranscriptSelections(IEnumerable<Document> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        foreach (var document in documents.ToArray())
        {
            document.ClearSelection();
        }
    }

    /// <remarks>
    /// Ctrl+P/Ctrl+N use the classic Emacs/readline history chords and avoid competing with the
    /// text input's built-in caret and selection handling for arrow keys.
    /// </remarks>
    private static bool IsControlChord(KeyEventArgs e, char letter) =>
        e.Stroke.Code == Code.Character &&
        e.Stroke.Character?.Value == letter &&
        (e.Stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock)) == Modifiers.Control;

    /// <summary>Determines whether one key event requests the global command palette.</summary>
    /// <param name="e">The nonnull routed key event.</param>
    /// <returns><see langword="true"/> for Ctrl+K or Ctrl+Shift+P, ignoring lock-state flags.</returns>
    internal static bool IsCommandPaletteChord(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Stroke.Code != Code.Character || e.Stroke.Character is not { } character)
        {
            return false;
        }

        var modifiers = e.Stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock);
        return (character.Value == 'k' && modifiers == Modifiers.Control) ||
            (character.Value == 'p' && modifiers == (Modifiers.Control | Modifiers.Shift));
    }

    private void RecallHistory(int direction)
    {
        if (_history.Count == 0)
        {
            return;
        }

        if (_historyIndex == _history.Count)
        {
            _historyDraft = _prompt.Text;
        }

        var nextIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count);
        _historyIndex = nextIndex;
        _prompt.Text = _historyIndex == _history.Count ? _historyDraft : _history[_historyIndex];
        _prompt.CaretIndex = _prompt.Text.Length;
    }

    private void StopTurn()
    {
        if (!_busy)
        {
            AppendEntry(new ChatEntry(ChatEntryKind.System, "Agent", "There is no active turn to stop."));
            return;
        }

        _turnCancellationRequested = true;
        _turnCancellation?.Cancel();
        _status.Content = "Stopping...";
    }

    private void SetPermissionMode(PermissionMode mode)
    {
        _permissions.Set(mode);
        RefreshStatusDetails();
        _status.Content = _busy ? "Running" : "Ready";
        RefreshPromptPlaceholder();
        RefreshMenuState();
        AppendEntry(new ChatEntry(
            ChatEntryKind.System,
            "Permissions changed",
            $"**{Text.Escape(PermissionModeCatalog.Title(mode))}** — {Text.Escape(PermissionModeCatalog.Description(mode))} The next protected tool call uses this mode."));
    }

    private void ScrollTranscript(int direction)
    {
        if (_entries.Count == 0)
        {
            return;
        }

        _followLatest = false;
        _ = _transcript.ScrollBy(0, direction * Math.Max(1, _transcript.Viewport.Height - 2), ScrollCause.Keyboard);
    }

    private void FollowLatest()
    {
        _followLatest = true;
        if (_entries.Count > 0)
        {
            ScrollToTailAfterLayout();
        }
    }

    private void ScrollToTailAfterLayout()
    {
        if (!_followLatest || _entries.Count == 0)
        {
            return;
        }

        _ = _transcript.BringIntoView(_transcript.Children[^1]);
        _ = _transcript.ScrollBy(
            0,
            _transcript.MaximumVerticalOffset - _transcript.VerticalOffset,
            ScrollCause.Programmatic);
    }

    private void RefreshResponsiveLayout(int width) =>
        _sidebar.Visibility = width >= 96 ? Visibility.Visible : Visibility.Collapsed;

    private async Task SubmitPromptAsync()
    {
        if (_busy)
        {
            return;
        }

        var userText = _prompt.Text.Trim();
        if (userText.Length == 0)
        {
            return;
        }

        _prompt.Text = "";
        if (_history.Count == 0 || _history[^1] != userText)
        {
            _history.Add(userText);
        }

        _historyIndex = _history.Count;
        _historyDraft = "";

        if (userText.StartsWith('/') && !userText.Contains('\n'))
        {
            // Presenting a modal while the submitting Enter is still inside preview routing lets
            // that stroke reach the new dialog's default button. Queue command dispatch so the
            // input boundary settles before a command changes focus or opens another surface.
            if (Dispatcher is { } dispatcher)
            {
                dispatcher.Post(() => ExecuteSlashCommand(userText));
            }
            else
            {
                ExecuteSlashCommand(userText);
            }

            return;
        }

        AppendEntry(new ChatEntry(ChatEntryKind.User, "", Text.Escape(userText)));
        SetBusy(true);
        _turnCancellationRequested = false;
        _turnCancellationRendered = false;
        _turnCancellation = new CancellationTokenSource();

        try
        {
            _conversation ??= CreateConversation();
            _assistantRow = null;
            _reasoningHasContent = false;
            _reasoningRow = _entries.Count;
            AppendEntry(new ChatEntry(ChatEntryKind.System, "Thinking", "Preparing the next step…", IsPending: true));
            _liveToolRows.Clear();
            _ = await _conversation.SendAsync(userText, this, _turnCancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            ReportTurnCancelled();
        }
        catch (Exception exception)
        {
            AppendEntry(new ChatEntry(ChatEntryKind.Error, "Error", Text.Escape(exception.Message)));
        }
        finally
        {
            if (_turnCancellationRequested)
            {
                ReportTurnCancelled();
            }

            _turnCancellation.Dispose();
            _turnCancellation = null;
            SetBusy(false);
        }
    }

    private async Task ShowSessionsAsync()
    {
        if (_busy || _pendingApproval is not null || _pendingQuestion is not null)
        {
            AppendEntry(new ChatEntry(ChatEntryKind.Error, "Session busy", "Stop the active interaction before browsing sessions."));
            return;
        }

        SetBusy(true);
        _status.Content = "Loading recent sessions...";
        try
        {
            _conversation ??= CreateConversation();
            var result = await _conversation.ListAsync(null, 10, CancellationToken.None).ConfigureAwait(true);
            if (result is ConversationSessionListUnavailable unavailable)
            {
                AppendEntry(new ChatEntry(ChatEntryKind.Error, "Sessions unavailable", Text.Escape(unavailable.SafeMessage)));
                return;
            }

            var page = (ConversationSessionPage) result;
            var body = page.Sessions.IsEmpty
                ? "No durable sessions exist for this workspace yet."
                : string.Join("\n", page.Sessions.Select(static (session, index) =>
                    $"{index + 1}. `{session.SessionId}` · {session.RecordedAt.ToLocalTime():g}"));
            AppendEntry(new ChatEntry(
                ChatEntryKind.System,
                "Recent sessions",
                $"{body}\n\nUse `/resume <session-id>` to open one."));
        }
        catch (Exception)
        {
            AppendEntry(new ChatEntry(ChatEntryKind.Error, "Sessions unavailable", "Recent sessions could not be loaded safely."));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ResumeSessionAsync(string commandLine)
    {
        if (_busy || _pendingApproval is not null || _pendingQuestion is not null)
        {
            AppendEntry(new ChatEntry(ChatEntryKind.Error, "Session busy", "Stop the active interaction before opening another session."));
            return;
        }

        var argument = commandLine.Split(' ', 2, StringSplitOptions.TrimEntries);
        if (argument.Length != 2 || !Guid.TryParse(argument[1], out var parsedId) || parsedId == Guid.Empty)
        {
            AppendEntry(new ChatEntry(ChatEntryKind.Error, "Session id required", "Use `/resume <session-id>` with an id from `/sessions`."));
            return;
        }

        SetBusy(true);
        _status.Content = "Opening durable session...";
        OwnedConversationSession? candidate = null;
        try
        {
            candidate = CreateConversation();
            var sessionId = new SessionId(parsedId);
            var opened = await candidate.OpenAsync(sessionId, CancellationToken.None).ConfigureAwait(true);
            if (opened is ConversationSessionOpenRejected rejected)
            {
                AppendEntry(new ChatEntry(ChatEntryKind.Error, "Session not opened", Text.Escape(rejected.SafeMessage)));
                return;
            }

            var history = await ReadCompleteHistoryAsync(candidate, CancellationToken.None).ConfigureAwait(true);
            var hydrated = await ProjectHistoryAsync(candidate, history, CancellationToken.None).ConfigureAwait(true);
            var previous = _conversation;
            _conversation = candidate;
            candidate = null;
            previous?.Dispose();
            _entries.Clear();
            _entries.Add(new ChatEntry(ChatEntryKind.System, "Resumed session", $"`{sessionId}`"));
            _entries.AddRange(hydrated);
            _usage.Reset();
            foreach (var assistant in history.OfType<AssistantMessage>()
                         .Where(static message => message.Response.Usage.ReportState != ModelUsageReportState.NotReported))
            {
                _usage.Add(assistant.Response.Usage);
            }
            _todoTitle = null;
            _todoItems = [];
            RefreshSidebarContext();
            RefreshSidebarTodo();
            RefreshTranscript();
        }
        catch (Exception)
        {
            AppendEntry(new ChatEntry(
                ChatEntryKind.Error,
                "Session not opened",
                "The session could not be opened and the current session is unchanged."));
        }
        finally
        {
            candidate?.Dispose();
            SetBusy(false);
        }
    }

    internal static async Task<ImmutableArray<AgentMessage>> ReadCompleteHistoryAsync(
        IConversationSession conversation,
        CancellationToken cancellationToken)
    {
        const int maximumPages = 64;
        var messages = ImmutableArray.CreateBuilder<AgentMessage>();
        var cursor = new SessionSequence(0);
        for (var pageIndex = 0; pageIndex < maximumPages; pageIndex++)
        {
            var result = await conversation.ReadHistoryAsync(cursor, 128, cancellationToken).ConfigureAwait(false);
            if (result is ConversationHistoryUnavailable unavailable)
            {
                throw new InvalidOperationException(unavailable.SafeMessage);
            }

            var page = (ConversationHistoryPage) result;
            messages.AddRange(page.Messages);
            if (page.Complete)
            {
                return messages.ToImmutable();
            }
            if (page.NextCursor.Value <= cursor.Value)
            {
                throw new InvalidOperationException("Session history pagination did not advance safely.");
            }
            cursor = page.NextCursor;
        }

        throw new InvalidOperationException("Session history exceeds the bounded hydration limit.");
    }

    internal static async Task<ImmutableArray<ChatEntry>> ProjectHistoryAsync(
        IConversationSession conversation,
        ImmutableArray<AgentMessage> messages,
        CancellationToken cancellationToken)
    {
        var entries = ImmutableArray.CreateBuilder<ChatEntry>();
        foreach (var message in messages)
        {
            foreach (var part in message.Parts)
            {
                switch (part)
                {
                    case TextPart text:
                        entries.Add(new ChatEntry(
                            message is UserMessage ? ChatEntryKind.User : message is AssistantMessage ? ChatEntryKind.Assistant : ChatEntryKind.System,
                            message is UserMessage or AssistantMessage ? "" : "Session",
                            Text.Escape(text.Text)));
                        break;
                    case ReasoningPart { Content.Text: { } reasoning }:
                        entries.Add(new ChatEntry(ChatEntryKind.System, "Thinking", Text.Escape(reasoning)));
                        break;
                    case ReasoningPart:
                        entries.Add(new ChatEntry(ChatEntryKind.System, "Thinking", "Stored reasoning is not visible."));
                        break;
                    case ToolCallPart call:
                        entries.Add(new ChatEntry(
                            ChatEntryKind.ToolCall,
                            $"Tool · {call.Tool.Name}",
                            "Stored tool request",
                            Presentation: await conversation.PresentToolAsync(call, cancellationToken).ConfigureAwait(false)));
                        break;
                    case ToolResultPart result:
                        var succeeded = result.Outcome.Kind == ToolCallOutcomeKind.Success;
                        entries.Add(new ChatEntry(
                            succeeded ? ChatEntryKind.ToolResultSuccess : ChatEntryKind.ToolResultFailure,
                            $"{(succeeded ? "Completed" : "Failed")} · {result.Tool.Name}",
                            succeeded ? "Stored tool result" : Text.Escape(result.Outcome.FailureReason ?? "The stored tool call failed."),
                            Presentation: await conversation.PresentToolAsync(result, cancellationToken).ConfigureAwait(false)));
                        break;
                    default:
                        entries.Add(new ChatEntry(ChatEntryKind.System, "Stored content", "This content type has no terminal projection."));
                        break;
                }
            }
        }
        return entries.ToImmutable();
    }

    private void ExecuteSlashCommand(string commandLine)
    {
        var name = commandLine.Split(' ', 2)[0];
        switch (name.ToLowerInvariant())
        {
            case "/help":
                ShowCommandReference(); break;
            case "/clear":
                if (_busy)
                {
                    AppendEntry(new ChatEntry(ChatEntryKind.Error, "Turn in progress", "Stop the current turn before clearing its live transcript."));
                    break;
                }

                _entries.Clear();
                RefreshTranscript();
                AppendEntry(new ChatEntry(ChatEntryKind.System, "Cleared", "Transcript cleared. The session continues."));
                break;
            case "/new":
                if (_busy)
                {
                    AppendEntry(new ChatEntry(ChatEntryKind.Error, "Session busy", "Stop the current turn before starting a new session."));
                    break;
                }

                _conversation?.Dispose();
                _conversation = null;
                _entries.Clear();
                RefreshTranscript();
                _usage.Reset();
                _todoTitle = null;
                _todoItems = [];
                RefreshSidebarContext();
                RefreshSidebarTodo();
                AppendEntry(new ChatEntry(ChatEntryKind.System, "New session", "Started a fresh session with no prior history."));
                break;
            case "/sessions":
                _ = ShowSessionsAsync();
                break;
            case "/resume":
                _ = ResumeSessionAsync(commandLine);
                break;
            case "/status":
                ShowAgentConfiguration(); break;
            case "/tools":
                ShowToolReference(); break;
            case "/keys":
                ShowKeyboardShortcuts(); break;
            case "/permissions":
                ExecutePermissionsCommand(commandLine.Split(' ', 2, StringSplitOptions.TrimEntries).ElementAtOrDefault(1));
                break;
            case "/model":
                ShowAgentConfiguration(); break;
            case "/workspace":
                ShowWorkspaceConfiguration(); break;
            case "/quit":
            case "/exit":
                _application?.Shutdown();
                break;
            default:
                AppendEntry(new ChatEntry(
                    ChatEntryKind.Error,
                    "Unknown command",
                    $"`{Text.Escape(name)}` is not a recognized command. Type `/help` for a list."));
                break;
        }
    }

    private OwnedConversationSession CreateConversation()
    {
        _status.Content = "Starting AgentKit runtime...";
        return AgentRuntime.Create(_workspaceRoot, OpenAiEnvironment.RequireApiKey(), _configuration, this, this, _permissions);
    }

    private void ShowAgentConfiguration() => RunDialog(async () =>
    {
        var edited = await AgentConfigurationDialog.ShowAsync(this, _configuration, _busy, CancellationToken.None);
        if (edited is not null && !_busy)
        {
            _configuration = edited;
            ApplyConfiguration("Agent configuration saved");
        }
    });

    private void ShowKeyboardShortcuts() => RunDialog(() =>
        MarkdownReferenceDialog.ShowAsync(this, KeyboardShortcutsReference.Title, KeyboardShortcutsReference.Markdown, CancellationToken.None));

    private void ShowCommandReference() => RunDialog(() =>
        MarkdownReferenceDialog.ShowAsync(this, CommandReference.Title, CommandReference.BuildMarkdown(SlashCommands.All), CancellationToken.None));

    private void ShowToolReference() => RunDialog(() =>
        MarkdownReferenceDialog.ShowAsync(this, ToolReference.Title, ToolReference.BuildMarkdown(_permissions.Mode), CancellationToken.None));

    private void ShowWorkspaceConfiguration()
    {
        if (_busy)
        {
            AppendEntry(new ChatEntry(ChatEntryKind.Error, "Turn in progress", "Stop the current turn before changing workspace access."));
            return;
        }

        RunDialog(async () =>
        {
            var roots = await WorkspaceConfigurationDialog.ShowAsync(
                this,
                _workspaceRoot,
                CodingAgentHostEnvironment.ToolchainRoots(),
                _configuration.ReadOnlyToolchainRoots,
                CancellationToken.None);
            if (roots is { } selected && !_busy)
            {
                _configuration = new CodingAgentConfiguration(
                    _configuration.ModelId,
                    _configuration.ReasoningEffort,
                    _configuration.MaximumTurns,
                    selected);
                ApplyConfiguration("Workspace configuration saved");
            }
        });
    }

    /// <summary>Runs one dialog presentation from a menu, palette, or slash-command path and
    /// surfaces a failed presentation in the transcript instead of losing it in a dropped task.</summary>
    /// <param name="present">The presentation, awaited on the dispatcher.</param>
    private void RunDialog(Func<Task> present)
    {
        Debug.Assert(present is not null, "A dialog presentation is required.");
        _ = PresentAsync();

        async Task PresentAsync()
        {
            try
            {
                await present();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                AppendEntry(new ChatEntry(ChatEntryKind.Error, "Dialog failed", Text.Escape(exception.Message)));
            }
        }
    }

    private void ExecutePermissionsCommand(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            var options = string.Join(
                "\n",
                PermissionModeCatalog.All.Select(mode =>
                    $"- `/permissions {PermissionModeCatalog.CommandArgument(mode)}` — **{Text.Escape(PermissionModeCatalog.Title(mode))}**{(mode == _permissions.Mode ? " (current)" : "")}. {Text.Escape(PermissionModeCatalog.Description(mode))}"));
            AppendEntry(new ChatEntry(ChatEntryKind.System, "Permissions", options));
            return;
        }

        if (!PermissionModeCatalog.TryParse(argument, out var requested))
        {
            AppendEntry(new ChatEntry(
                ChatEntryKind.Error,
                "Unknown permission mode",
                $"`{Text.Escape(argument)}` is not a mode. Use {string.Join(", ", PermissionModeCatalog.All.Select(mode => $"`{PermissionModeCatalog.CommandArgument(mode)}`"))}."));
            return;
        }

        SetPermissionMode(requested);
    }

    private void SetModel(string modelId)
    {
        Debug.Assert(
            CodingAgentConfiguration.Models.Any(option => string.Equals(option.ModelId, modelId, StringComparison.Ordinal)),
            "A model menu callback must carry an identifier from the captured model catalog.");
        if (_busy || IsCurrentModel(modelId))
        {
            return;
        }

        _configuration = new CodingAgentConfiguration(
            modelId,
            _configuration.ReasoningEffort,
            _configuration.MaximumTurns,
            _configuration.ReadOnlyToolchainRoots);
        ApplyConfiguration("Model changed");
    }

    private void SetReasoningEffort(LlmReasoningEffort effort)
    {
        Debug.Assert(Enum.IsDefined(effort), "A reasoning menu callback must carry a defined effort.");
        if (_busy || _configuration.ReasoningEffort == effort)
        {
            return;
        }

        _configuration = new CodingAgentConfiguration(
            _configuration.ModelId,
            effort,
            _configuration.MaximumTurns,
            _configuration.ReadOnlyToolchainRoots);
        ApplyConfiguration("Reasoning effort changed");
    }

    private void ApplyConfiguration(string message)
    {
        _conversation?.Dispose();
        _conversation = null;
        RefreshStatusDetails();
        _usage.Reset();
        RefreshSidebarContext();
        AppendEntry(new ChatEntry(
            ChatEntryKind.System,
            message,
            $"New session ready · `{Text.Escape(_configuration.ModelId)}` · {AgentConfigurationDialog.ReasoningLabel(_configuration.ReasoningEffort)} · {Text.Escape(_permissions.Label())}."));
        _status.Content = "Ready";
        RefreshPromptPlaceholder();
        RefreshMenuState();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This is called from deep inside the running turn's own call stack (the agent loop's tool-call loop),
    /// never from the UI-thread continuation <see cref="SubmitPromptAsync"/> resumes on, because AgentKit's
    /// internal awaits use <c>ConfigureAwait(false)</c>. Every UI mutation here is therefore explicitly marshaled
    /// through <see cref="ControlBase.Dispatcher"/> rather than assumed to already run on it.
    /// </remarks>
    public async Task<bool> ConfirmAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var approval = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previousStatus = "";
        await Dispatcher!.InvokeAsync(
            () =>
            {
                _pendingApprovalToolKey = request.Binding.Request.ToolCallId?.ToString();
                var hasPresentedRequest = false;
                if (_pendingApprovalToolKey is { Length: > 0 } toolKey && _liveToolRows.TryGetValue(toolKey, out var approvalRow))
                {
                    hasPresentedRequest = _entries[approvalRow].Presentation is not null;
                    ReplaceEntry(approvalRow, _entries[approvalRow] with { HeaderText = "Awaiting approval" });
                }

                if (!hasPresentedRequest)
                {
                    AppendEntry(new ChatEntry(
                        ChatEntryKind.System,
                        "⚠ Approval required",
                        Text.Escape(request.SafePresentation)));
                }

                AppendEntry(new ChatEntry(
                    ChatEntryKind.System,
                    "Your decision",
                    $"Press **Enter**/**y** to allow, **Esc**/**n** to deny.\n\nExpires at `{Text.Escape(request.Binding.ExpiresAt.ToLocalTime().ToString("T", CultureInfo.CurrentCulture))}`."));
                previousStatus = _status.Content;
                _status.Content = "Approval required (Enter/y = allow, Esc/n = deny)";

                _pendingApproval = approval;
                _prompt.IsReadOnly = false;
                _prompt.Placeholder = "Enter/y allow · Esc/n deny (your draft is preserved)";
            },
            cancellationToken).ConfigureAwait(false);

        var remaining = request.Binding.ExpiresAt - DateTimeOffset.UtcNow;
        using var deadline = new CancellationTokenSource(
            remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
            TimeProvider.System);
        using var deadlineRegistration = deadline.Token.Register(
            static state => ((TaskCompletionSource<bool>) state!).TrySetResult(false), approval);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<bool>) state!).TrySetResult(false), approval);
        var approved = await approval.Task.ConfigureAwait(false);
        var expired = deadline.IsCancellationRequested || DateTimeOffset.UtcNow >= request.Binding.ExpiresAt;
        approved &= !expired;

        await Dispatcher!.InvokeAsync(
            () =>
            {
                AppendEntry(new ChatEntry(
                    approved ? ChatEntryKind.System : ChatEntryKind.ToolResultFailure,
                    expired ? "Approval expired" : approved ? "Approval submitted" : "Denied",
                    expired
                        ? "The deadline passed before the authority accepted a response."
                        : approved
                            ? "The security authority is validating the retained request."
                            : "The action was skipped."));
                _pendingApproval = null;
                if (_pendingApprovalToolKey is { Length: > 0 } toolKey && _liveToolRows.TryGetValue(toolKey, out var approvalRow))
                {
                    ReplaceEntry(
                        approvalRow,
                        _entries[approvalRow] with
                        {
                            HeaderText = expired ? "Approval expired" : approved ? "Approval submitted" : "Denied",
                            IsPending = approved,
                        });
                }

                _pendingApprovalToolKey = null;
                _prompt.IsReadOnly = _busy;
                RefreshPromptPlaceholder();
                _status.Content = previousStatus;
            },
            CancellationToken.None).ConfigureAwait(false);
        return approved;
    }

    /// <inheritdoc/>
    public async ValueTask<HumanQuestionSelection> AskAsync(
        HumanQuestionPrompt prompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var answer = new TaskCompletionSource<HumanQuestionSelection>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Dispatcher!.InvokeAsync(
            () =>
            {
                if (_pendingQuestion is not null || _pendingApproval is not null)
                {
                    _ = answer.TrySetException(new InvalidOperationException("Another terminal interaction is already active."));
                    return;
                }

                _pendingQuestion = answer;
                _pendingQuestionPrompt = prompt;
                var questionKey = prompt.ToolCallId.ToString();
                if (_liveToolRows.TryGetValue(questionKey, out var questionRow))
                {
                    ReplaceEntry(questionRow, _entries[questionRow] with { HeaderText = "Awaiting your answer" });
                }
                _questionDraft = _prompt.Text;
                _prompt.Text = "";
                var options = string.Join("\n", prompt.Options.Select(
                    static (option, index) => $"{index + 1}. **{Text.Escape(option.Label)}** — {Text.Escape(option.Description)}"));
                AppendEntry(new ChatEntry(
                    ChatEntryKind.System,
                    "Answer needed",
                    $"{Text.Escape(prompt.Prompt)}\n\n{options}"));
                AppendEntry(new ChatEntry(
                    ChatEntryKind.System,
                    "Your answer",
                    prompt.AllowsFreeText
                        ? "Enter an option number, then optional text. Press **Enter** to answer; **Shift+Enter** adds a line; **Esc** cancels the turn."
                        : "Enter one option number. Press **Enter** to answer; **Esc** cancels the turn."));
                _prompt.IsReadOnly = false;
                _prompt.Placeholder = prompt.AllowsFreeText
                    ? $"Choose 1–{prompt.Options.Length}, then optional text · Enter answers · Shift+Enter adds a line"
                    : $"Choose 1–{prompt.Options.Length} · Enter answers";
                _status.Content = "Waiting for your answer (Esc stops the turn)";
                _ = _application?.Focus.Focus(_prompt);
            },
            cancellationToken).ConfigureAwait(false);

        try
        {
            return await answer.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await Dispatcher!.InvokeAsync(
                () =>
                {
                    if (ReferenceEquals(_pendingQuestion, answer))
                    {
                        _pendingQuestion = null;
                        _pendingQuestionPrompt = null;
                        _prompt.Text = _questionDraft;
                        _prompt.CaretIndex = _prompt.Text.Length;
                        _questionDraft = "";
                        _prompt.IsReadOnly = _busy && _pendingApproval is null;
                        RefreshPromptPlaceholder();
                        _status.Content = _busy ? "Thinking... (Esc to stop)" : "Ready";
                    }
                },
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private void TrySubmitQuestionAnswer()
    {
        Debug.Assert(_pendingQuestion is not null, "Question submission requires an active question wait.");
        Debug.Assert(_pendingQuestionPrompt is not null, "Question submission requires the exact displayed prompt.");
        var prompt = _pendingQuestionPrompt;
        if (!HumanQuestionSelectionParser.TryParse(
                _prompt.Text,
                prompt.Options,
                prompt.AllowsFreeText,
                out var selection))
        {
            _status.Content = prompt.AllowsFreeText
                ? $"Choose an option from 1 to {prompt.Options.Length}."
                : $"Choose only one option from 1 to {prompt.Options.Length}.";
            return;
        }

        var option = prompt.Options.Single(candidate => candidate.Id == selection.OptionId);
        AppendEntry(new ChatEntry(
            ChatEntryKind.User,
            "You answered",
            $"**{Text.Escape(option.Label)}**{(selection.FreeText is null ? "" : $"\n\n{Text.Escape(selection.FreeText)}")}"));
        _ = _pendingQuestion.TrySetResult(selection);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _prompt.IsReadOnly = busy && _pendingApproval is null;
        _spinner.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _status.Content = busy ? "Thinking... (Esc to stop)" : "Ready";
        if (!busy)
        {
            RefreshPromptPlaceholder();
        }

        RefreshMenuState();
    }

    private void RefreshMenuState()
    {
        if (_newSessionMenuItem is { } newSession)
        {
            newSession.IsEnabled = !_busy;
        }

        if (_clearTranscriptMenuItem is { } clearTranscript)
        {
            clearTranscript.IsEnabled = !_busy;
        }

        if (_recentSessionsMenuItem is { } recentSessions)
        {
            recentSessions.IsEnabled = !_busy && _pendingApproval is null && _pendingQuestion is null;
        }

        if (_stopTurnMenuItem is { } stopTurn)
        {
            stopTurn.IsEnabled = _busy;
        }

        if (_modelMenuItem is { } modelMenu)
        {
            modelMenu.Text = ModelMenuLabel(_configuration);
            modelMenu.IsEnabled = !_busy;
        }

        if (_reasoningMenuItem is { } reasoningMenu)
        {
            reasoningMenu.Text = ReasoningMenuLabel(_configuration.ReasoningEffort);
            reasoningMenu.IsEnabled = !_busy;
        }

        for (var index = 0; index < _modelMenuItems.Length; index++)
        {
            _modelMenuItems[index].IsChecked = IsCurrentModel(CodingAgentConfiguration.Models[index].ModelId);
        }

        for (var index = 0; index < _reasoningMenuItems.Length; index++)
        {
            _reasoningMenuItems[index].IsChecked =
                CodingAgentConfiguration.ReasoningEfforts[index] == _configuration.ReasoningEffort;
        }

        for (var index = 0; index < _permissionMenuItems.Length; index++)
        {
            _permissionMenuItems[index].IsChecked = PermissionModeCatalog.All[index] == _permissions.Mode;
        }
    }

    private void RefreshPromptPlaceholder() =>
        _prompt.Placeholder = _permissions.Mode == PermissionMode.ReadOnly
            ? "Ask the coding agent (read-only), or press / for commands... (Enter sends · Shift+Enter adds a line)"
            : "Ask the coding agent, or press / for commands... (Enter sends · Shift+Enter adds a line)";

    /// <inheritdoc/>
    public ValueTask OnEventAsync(ConversationEvent conversationEvent, CancellationToken cancellationToken) =>
        Dispatcher!.InvokeAsync(() => AppendLiveEvent(conversationEvent), cancellationToken);

    private void AppendLiveEvent(ConversationEvent conversationEvent)
    {
        switch (conversationEvent)
        {
            case ConversationAssistantTextDeltaEvent text:
                FinalizeReasoning();
                _assistantRow = AppendDelta(_assistantRow, ChatEntryKind.Assistant, "", text.Text);
                break;
            case ConversationAssistantTextEvent text when _assistantRow is null:
                FinalizeReasoning();
                _assistantRow = AppendDelta(null, ChatEntryKind.Assistant, "", text.Text);
                break;
            case ConversationReasoningEvent reasoning:
                if (!_reasoningHasContent && _reasoningRow is { } placeholderRow)
                {
                    ReplaceEntry(placeholderRow, _entries[placeholderRow] with { Body = "" });
                }

                _reasoningHasContent = true;
                _reasoningRow = AppendDelta(_reasoningRow, ChatEntryKind.System, "Thinking", reasoning.Text, pending: true);
                break;
            case ConversationToolCallEvent call:
                var callKey = call.CallId.ToString();
                FinalizeReasoning();
                _assistantRow = null;
                _liveToolRows[callKey] = _entries.Count;
                AppendEntry(new ChatEntry(
                    ChatEntryKind.ToolCall,
                    $"Running {Text.Escape(call.ToolName)}",
                    call.Presentation is null ? "Tool request received." : "",
                    IsPending: true,
                    Presentation: call.Presentation));
                break;
            case ConversationToolResultEvent result:
                var resultKey = result.CallId.ToString();
                if (_liveToolRows.Remove(resultKey, out var row))
                {
                    ReplaceEntry(row, BuildToolResultEntry(result));
                }
                else
                {
                    AppendEntry(BuildToolResultEntry(result));
                }

                if (result.Succeeded && result.ToolName is "todo" or "plan" &&
                    TodoState.TryParse(result.Summary, out var title, out var items))
                {
                    _todoTitle = title;
                    _todoItems = items;
                    RefreshSidebarTodo();
                }

                _assistantRow = null;
                _reasoningRow = null;
                break;
            case ConversationUsageEvent usage:
                _usage.Add(usage.Usage);
                RefreshSidebarContext();
                break;
            case ConversationTurnCompletedEvent completed:
                if (string.Equals(completed.Outcome, "cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    ReportTurnCancelled();
                    _status.Content = "Cancelled";
                    break;
                }

                FinalizeReasoning();
                if (!completed.Succeeded)
                {
                    AppendEntry(new ChatEntry(ChatEntryKind.Error, "Turn failed", Text.Escape(completed.Outcome.ToString())));
                }

                _status.Content = completed.Succeeded ? "Finishing..." : $"Failed: {completed.Outcome}";
                break;
            default:
                break;
        }
    }

    private void ReportTurnCancelled()
    {
        if (_turnCancellationRendered)
        {
            return;
        }

        _turnCancellationRendered = true;
        var cancelled = new ChatEntry(ChatEntryKind.System, "Cancelled", "Interrupted by Escape. The session continues.");
        if (_reasoningRow is { } row && !_reasoningHasContent)
        {
            ReplaceEntry(row, cancelled);
            _reasoningRow = null;
            _reasoningHasContent = false;
        }
        else
        {
            FinalizeReasoning();
            AppendEntry(cancelled);
        }

        _assistantRow = null;
    }

    private void FinalizeReasoning()
    {
        if (_reasoningRow is { } reasoningRow)
        {
            ReplaceEntry(reasoningRow, _entries[reasoningRow] with { IsPending = false, HeaderText = "Thought" });
            _reasoningRow = null;
            _reasoningHasContent = false;
        }
    }

    private int AppendDelta(int? row, ChatEntryKind kind, string header, string delta, bool pending = false)
    {
        if (row is { } index)
        {
            ReplaceEntry(index, _entries[index] with { Body = _entries[index].Body + delta });
            return index;
        }

        var newIndex = _entries.Count;
        AppendEntry(new ChatEntry(kind, header, delta, IsPending: pending));
        return newIndex;
    }

    private static ChatEntry BuildToolResultEntry(ConversationToolResultEvent result)
    {
        var header = (result.Succeeded ? "✓ " : "✗ ") + Text.Escape(result.ToolName);
        return result.Presentation is not null
            ? new ChatEntry(
                result.Succeeded ? ChatEntryKind.ToolResultSuccess : ChatEntryKind.ToolResultFailure,
                header,
                "",
                Presentation: result.Presentation)
            : new ChatEntry(
                result.Succeeded ? ChatEntryKind.ToolResultSuccess : ChatEntryKind.ToolResultFailure,
                header,
                Text.Escape(Truncate(result.Summary, result.Succeeded ? 8_000 : 2_000)),
                IsCode: result.Succeeded);
    }

    private void RefreshSidebarContext()
    {
        var summary = _usage.Format();
        _sidebarContext.Content = summary is null ? "<d>No usage reported yet.</d>" : $"<d>{summary}</d>";
        _usageStatus.Content = summary is null ? "<d>usage –</d>" : $"<d>{Text.Escape(summary)}</d>";
    }

    private void RefreshStatusDetails()
    {
        _permissionStatus.Content = $"<d>{Text.Escape(_permissions.Label())}</d>";
        _modelStatus.Content = $"<d>{Text.Escape(ModelStatusLabel(_configuration))}</d>";
    }

    private void RefreshSidebarTodo()
    {
        _sidebarTodo.Children.Clear();
        if (_todoTitle is { Length: > 0 })
        {
            _sidebarTodo.Children.Add(new Text($"<b>{Text.Escape(_todoTitle)}</b>"));
        }

        if (_todoItems.IsEmpty)
        {
            _sidebarTodo.Children.Add(new Text("<d>No active tasks.</d>"));
            return;
        }

        foreach (var item in _todoItems)
        {
            var tag = TodoTagFor(item.Status);
            _sidebarTodo.Children.Add(new Text(
                $"<{tag}>{TodoGlyphFor(item.Status)}</{tag}> {Text.Escape(item.Text)}"));
        }
    }

    private static string TodoGlyphFor(TodoStatus status) => status switch
    {
        TodoStatus.Pending => "☐",
        TodoStatus.InProgress => "◐",
        TodoStatus.Completed => "☑",
        TodoStatus.Blocked => "✗",
        _ => "☐",
    };

    private static string TodoTagFor(TodoStatus status) => status switch
    {
        TodoStatus.Pending => "d",
        TodoStatus.InProgress => "accent",
        TodoStatus.Completed => "success",
        TodoStatus.Blocked => "error",
        _ => "d",
    };

    private void AppendEntry(ChatEntry entry)
    {
        _entries.Add(entry);
        RefreshTranscript();
    }

    private void ReplaceEntry(int index, ChatEntry entry)
    {
        _entries[index] = entry;
        RefreshTranscript();
        if (_followLatest)
        {
            ScrollToTailAfterLayout();
        }
    }

    private void RefreshTranscript()
    {
        if (_selectedTranscriptRows.Count > 0)
        {
            // Preserve the selected row's semantic source while still exposing later activity.
            while (_transcript.Children.Count < _entries.Count)
            {
                _transcript.Children.Add(BuildEntryView(_entries[_transcript.Children.Count]));
            }

            _transcriptRefreshPending = _transcript.Children.Count == _entries.Count;
            if (_followLatest)
            {
                ScrollToTailAfterLayout();
                Dispatcher?.Post(ScrollToTailAfterLayout);
            }
            return;
        }

        _transcript.Children.Clear();
        foreach (var entry in _entries)
        {
            _transcript.Children.Add(BuildEntryView(entry));
        }
        if (_followLatest && _entries.Count > 0)
        {
            ScrollToTailAfterLayout();
            Dispatcher?.Post(ScrollToTailAfterLayout);
        }
    }

    /// <summary>Builds one retained row whose Document owns pointer selection across its semantic body.</summary>
    /// <param name="entry">The immutable transcript entry to project.</param>
    /// <returns>Nonselecting visual chrome containing one selectable Document.</returns>
    internal Stack BuildEntryView(ChatEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ControlBase? header = null;
        if (entry.HeaderText.Length > 0)
        {
            var tag = MarkupTagFor(entry.Kind);
            var headerText = new Text($"<{tag}><b>{Text.Escape(entry.HeaderText)}</b></{tag}>")
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            header = entry.IsPending
                ? new Stack
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 1,
                    Children = { new Spinner(), headerText },
                }
                : headerText;
        }

        var row = new Document
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Height = Length.Auto,
            IsFocusable = true,
            IsTabStop = true,
            IsTextSelectionEnabled = true,
        };
        if (entry.Presentation is not null)
        {
            if (header is not null)
            {
                row.Blocks.Add(new DocumentBlockControl(header));
            }

            AddPresentationBlocks(row, entry.Presentation);
        }
        else if (!entry.IsCode)
        {
            _ = row.Load(entry.Body, _markdownReader);
            if (header is not null)
            {
                row.Blocks.Insert(0, new DocumentBlockControl(header));
            }
        }
        else
        {
            if (header is not null)
            {
                row.Blocks.Add(new DocumentBlockControl(header));
            }

            row.Blocks.Add(new DocumentBlockControl(BuildEntryBody(entry)));
        }
        row.TextSelectionChanged += (_, e) => OnTranscriptRowSelectionChanged(row, e.Selection.IsEmpty);
        return new Stack
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(1, 0, 0, 0),
            Border = new Border(
                BorderSide.Left,
                BorderGlyphStyle.Heavy,
                AccentColorFor(entry.Kind),
                Color.Transparent,
                SemanticDecoration.Border),
            IsFocusable = false,
            IsTabStop = false,
            IsTextSelectionEnabled = false,
            Children = { row },
        };
    }

    private void OnTranscriptRowSelectionChanged(ControlBase row, bool isEmpty)
    {
        _ = isEmpty ? _selectedTranscriptRows.Remove(row) : _selectedTranscriptRows.Add(row);

        if (_selectedTranscriptRows.Count == 0 && _transcriptRefreshPending)
        {
            _transcriptRefreshPending = false;
            RefreshTranscript();
        }
    }

    /// <remarks>
    /// Markdown prose joins adjacent non-blank lines into one paragraph unless every line ends with a hard
    /// break, which is exactly wrong for source or command output. Standalone code entries therefore use a
    /// <see cref="CodeView"/>. Tool presentations are projected into native document blocks so their complete
    /// formatter-bounded height belongs to the transcript rather than to a nested scrolling viewport.
    /// </remarks>
    private static CodeView BuildEntryBody(ChatEntry entry) => new()
    {
        Code = entry.Body,
        Language = entry.Language,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Overflow = Overflow.Wrap,
        IsFoldingEnabled = false,
        // The containing row owns one selection stream across its header and body.
        IsFocusable = false,
        IsTabStop = false,
    };

    private static void AddPresentationBlocks(Document document, ToolPresentation presentation)
    {
        foreach (var part in presentation.Parts)
        {
            if (part.Kind == ToolPresentationPartKind.Text)
            {
                document.Blocks.Add(new DocumentParagraph(Text.Escape(part.Text)));
                continue;
            }

            if (part.Kind == ToolPresentationPartKind.Diff && part.Path is { Length: > 0 })
            {
                document.Blocks.Add(new DocumentParagraph($"<d>{Text.Escape(part.Path)}</d>"));
            }

            document.Blocks.Add(new DocumentCodeBlock(part.Text)
            {
                Language = part.Kind == ToolPresentationPartKind.Diff ? string.Empty : part.Language ?? string.Empty,
            });
        }

        if (presentation.OmittedCharacters > 0)
        {
            document.Blocks.Add(new DocumentParagraph($"<d>… {presentation.OmittedCharacters:N0} characters omitted</d>"));
        }
    }

    private static SemanticColor AccentColorFor(ChatEntryKind kind) => kind switch
    {
        ChatEntryKind.User => SemanticColor.Accent,
        ChatEntryKind.Assistant => SemanticColor.Info,
        ChatEntryKind.ToolCall => SemanticColor.Muted,
        ChatEntryKind.ToolResultSuccess => SemanticColor.Success,
        ChatEntryKind.ToolResultFailure => SemanticColor.Error,
        ChatEntryKind.Error => SemanticColor.Error,
        ChatEntryKind.System => SemanticColor.Muted,
        _ => SemanticColor.Muted,
    };

    private static string MarkupTagFor(ChatEntryKind kind) => kind switch
    {
        ChatEntryKind.User => "accent",
        ChatEntryKind.Assistant => "info",
        ChatEntryKind.ToolCall => "d",
        ChatEntryKind.ToolResultSuccess => "success",
        ChatEntryKind.ToolResultFailure => "error",
        ChatEntryKind.Error => "error",
        ChatEntryKind.System => "d",
        _ => "d",
    };

    private static string WorkspaceLabel(string workspaceRoot)
    {
        var trimmed = workspaceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return name.Length == 0 ? trimmed : name;
    }

    private static string Truncate(string text, int maximumLength) =>
        text.Length <= maximumLength ? text : string.Concat(text.AsSpan(0, maximumLength), "...");
}

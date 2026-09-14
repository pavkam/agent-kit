// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using System.Collections.Frozen;
using System.Text.Json;

using SharpVision.Controls.Document;
using SharpVision.Controls.SyntaxHighlighting;
using SharpVision.Documents.Markdown;
using SharpVision.Terminal.Input;

/// <summary>The coding agent's chat screen: a menu bar, a scrollable message list, a status bar, and a
/// command-palette-driven prompt.</summary>
internal sealed class ChatScreen: Screen, IApprovalPrompt
{
    private readonly List<ChatEntry> _entries = [];
    private readonly ListView _transcript = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        SelectionMode = ListSelectionMode.None,
        RowHeight = Length.Auto,
        // A read-only transcript is never a keyboard target itself: it must not steal focus (and
        // with it, arrow-key routing) from the always-focused prompt below.
        IsFocusable = false,
        IsTabStop = false,
    };

    private readonly TextInput _prompt = new()
    {
        Placeholder = "Ask the coding agent, or type / for commands... (Enter to send)",
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private readonly ListView _commandList = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        SelectionMode = ListSelectionMode.Single,
        RowHeight = Length.Auto,
        // Navigated manually from the prompt's KeyDown handler below; it must never take focus
        // itself, for the same reason as the transcript above.
        IsFocusable = false,
        IsTabStop = false,
    };

    private readonly Popup _commandPopup;
    private readonly Spinner _spinner = new() { Visibility = Visibility.Collapsed };
    private readonly Text _status = new("Ready");
    private readonly Text _usageStatus = new("<d>–</d>");
    private readonly Text _sidebarContext = new("<d>No usage reported yet.</d>");
    private readonly Stack _sidebarTodo = new() { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly MarkdownDocumentReader _markdownReader = new();
    private readonly SessionUsage _usage = new();
    private readonly string _workspaceRoot;

    private readonly List<string> _history = [];

    /// <summary>Each tool's most recent unmatched call arguments, in call order, so a result event (which
    /// carries no call id of its own) can still be correlated back to what it was called with — needed to
    /// recover a <c>read_file</c> call's path for syntax-highlighting its result.</summary>
    private readonly Dictionary<string, Queue<string>> _pendingToolArguments = [];

    private Application? _application;
    private IConversationSession? _conversation;
    private CancellationTokenSource? _turnCancellation;
    private TaskCompletionSource<bool>? _pendingApproval;
    private ImmutableArray<SlashCommand> _commandMatches = [];
    private ImmutableArray<TodoItem> _todoItems = [];
    private string? _todoTitle;
    private int _commandSelectedIndex;
    private bool _busy;
    private int _historyIndex;
    private string _historyDraft = "";

    public ChatScreen(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
        AppendEntry(new ChatEntry(ChatEntryKind.System, "Workspace", Text.Escape(workspaceRoot)));

        _commandPopup = new Popup
        {
            Anchor = _prompt,
            Placement = PopupPlacement.Below,
            FocusOnOpen = false,
            ModalBehavior = PopupModalBehavior.None,
            CloseOnEscape = false,
            ConnectsToAnchor = true,
            Width = Length.Cells(72),
            Content = BuildCommandPopupContent(),
        };

        var menuBar = BuildMenuBar();
        // Reachable by Alt+mnemonic without joining ordinary Tab traversal or initial focus - the
        // prompt below must always keep focus by default, exactly like SharpVision's own
        // TextEditor sample (EditorScreen.cs) does for its menu bar.
        menuBar.IsTabStop = false;
        var menuBarFrame = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(1, 0),
            Border = new Border(
                BorderSide.Bottom,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Children = { menuBar },
        };

        var statusBar = new StatusBar { Padding = new Thickness(1, 0) };
        statusBar.Items.Add(new StatusBarItem
        {
            Content = new Stack
            {
                Orientation = Orientation.Horizontal,
                Spacing = 1,
                Children = { _spinner, _status },
            },
        });
        statusBar.Items.Add(new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            ShowLeftSeparator = true,
            Content = _usageStatus,
        });
        statusBar.Items.Add(new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            ShowLeftSeparator = true,
            Content = new Text($"<d>{Text.Escape(OpenAiEnvironment.ModelId())}</d>"),
        });
        statusBar.Items.Add(new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            ShowLeftSeparator = true,
            Content = new Text($"<d>{Text.Escape(WorkspaceLabel(workspaceRoot))}</d>"),
        });

        var promptRow = new Dock { HorizontalAlignment = HorizontalAlignment.Stretch };
        promptRow.Children.Add(_prompt);

        var sidebar = BuildSidebar();

        var layout = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Dock.SetSide(menuBarFrame, DockSide.Top);
        Dock.SetSide(statusBar, DockSide.Bottom);
        Dock.SetSide(promptRow, DockSide.Bottom);
        Dock.SetSide(sidebar, DockSide.Right);
        layout.Children.Add(menuBarFrame);
        layout.Children.Add(statusBar);
        layout.Children.Add(promptRow);
        layout.Children.Add(sidebar);
        layout.Children.Add(_transcript);

        var root = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        root.Children.Add(layout);
        root.Children.Add(_commandPopup);

        _transcript.ItemTemplate = new ItemTemplate(BuildEntryView);
        _commandList.ItemTemplate = new ItemTemplate(BuildCommandRow);
        _prompt.Submitted += OnPromptSubmittedAsync;
        _prompt.TextChanged += OnPromptTextChanged;
        _prompt.KeyDown += OnPromptKeyDown;

        InitializeContent(root);
    }

    protected override void OnStarted(Application application)
    {
        _application = application;
        _ = application.Focus.Focus(_prompt);
    }

    private Menu BuildMenuBar() =>
        MenuBuilder.Horizontal(spacing: 2)
            .Submenu(
                "&Session",
                session => session
                    .Item("&New session", onInvoke: () => ExecuteSlashCommand("/new"))
                    .Item("&Clear transcript", onInvoke: () => ExecuteSlashCommand("/clear"))
                    .Separator()
                    .Item("&Workspace info", onInvoke: () => ExecuteSlashCommand("/workspace"))
                    .Item("&Model info", onInvoke: () => ExecuteSlashCommand("/model"))
                    .Separator()
                    .Item("&Quit", shortcut: "Ctrl+Q", onInvoke: () => _application?.Shutdown()))
            .Submenu(
                "&Help",
                help => help.Item("&Commands", onInvoke: () => ExecuteSlashCommand("/help")))
            .Build();

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

    private Dock BuildCommandPopupContent()
    {
        var title = new Text("<accent><b>Commands</b></accent>") { HorizontalAlignment = HorizontalAlignment.Left };
        var hint = new Text("<d>Ctrl+P/N · Tab/Enter · Esc</d>") { HorizontalAlignment = HorizontalAlignment.Right };
        Dock.SetSide(hint, DockSide.Right);

        var headerRow = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(1, 0),
            Border = new Border(
                BorderSide.Bottom,
                BorderGlyphStyle.Light,
                SemanticColor.ControlBorder,
                Color.Transparent,
                SemanticDecoration.Border),
            Children = { hint, title },
        };

        Dock.SetSide(headerRow, DockSide.Top);
        var body = new Dock { HorizontalAlignment = HorizontalAlignment.Stretch };
        body.Children.Add(headerRow);
        body.Children.Add(_commandList);
        return body;
    }

    private static Stack BuildCommandRow(object? item)
    {
        var command = (SlashCommand) item!;
        return new Stack
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(1, 0),
            Children =
            {
                new Text($"<accent><b>{Text.Escape(command.Name)}</b></accent>"),
                new Text($"<d>{Text.Escape(command.Description)}</d>"),
            },
        };
    }

    private void OnPromptTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = _prompt.Text;
        if (!text.StartsWith('/'))
        {
            CloseCommandPopup();
            return;
        }

        _commandMatches = [.. SlashCommands.Match(text)];
        if (_commandMatches.IsEmpty)
        {
            CloseCommandPopup();
            return;
        }

        _commandSelectedIndex = 0;
        _commandList.Items = [.. _commandMatches];
        _commandList.SelectedIndex = 0;
        _commandPopup.IsOpen = true;
    }

    private void MoveCommandSelection(int direction)
    {
        if (_commandMatches.IsEmpty)
        {
            return;
        }

        _commandSelectedIndex = Math.Clamp(_commandSelectedIndex + direction, 0, _commandMatches.Length - 1);
        _commandList.SelectedIndex = _commandSelectedIndex;
        _ = _commandList.BringIntoView(_commandSelectedIndex);
    }

    private void AcceptSelectedCommand()
    {
        if (_commandMatches.IsEmpty)
        {
            CloseCommandPopup();
            return;
        }

        var command = _commandMatches[_commandSelectedIndex];
        CloseCommandPopup();
        _prompt.Text = command.Name + " ";
        _prompt.CaretIndex = _prompt.Text.Length;
    }

    private void CloseCommandPopup()
    {
        _commandMatches = [];
        _commandSelectedIndex = 0;
        if (_commandPopup.IsOpen)
        {
            _commandPopup.IsOpen = false;
        }
    }

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
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

        if (_busy)
        {
            if (e.Stroke.Code == Code.Escape)
            {
                _turnCancellation?.Cancel();
                e.IsHandled = true;
            }

            return;
        }

        if (_commandPopup.IsOpen)
        {
            if (IsControlChord(e, 'p'))
            {
                MoveCommandSelection(-1);
                e.IsHandled = true;
                return;
            }

            if (IsControlChord(e, 'n'))
            {
                MoveCommandSelection(1);
                e.IsHandled = true;
                return;
            }

            if (e.Stroke.Code == Code.Enter &&
                string.Equals(_prompt.Text, _commandMatches[_commandSelectedIndex].Name, StringComparison.OrdinalIgnoreCase))
            {
                // The user already typed the whole command themselves: submit it directly instead
                // of making them press Enter twice (once to "accept" a match they didn't need).
                CloseCommandPopup();
                return;
            }

            if (e.Stroke.Code is Code.Enter or Code.Tab)
            {
                AcceptSelectedCommand();
                e.IsHandled = true;
                return;
            }

            if (e.Stroke.Code == Code.Escape)
            {
                CloseCommandPopup();
                e.IsHandled = true;
                return;
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

    /// <remarks>
    /// Deliberately not Up/Down: SharpVision's <see cref="Menu"/> unconditionally claims bare
    /// arrow keys for its own navigation the moment one exists anywhere in the attached tree,
    /// regardless of focus - so the menu bar's mere presence would otherwise swallow these before
    /// they ever reach the prompt. Ctrl+P/Ctrl+N (the classic Emacs/readline history chords) sidestep
    /// that entirely and match the vocabulary coding-CLI users already know.
    /// </remarks>
    private static bool IsControlChord(KeyEventArgs e, char letter) =>
        e.Stroke.Code == Code.Character &&
        e.Stroke.Character?.Value == letter &&
        (e.Stroke.Modifiers & ~(Modifiers.CapsLock | Modifiers.NumLock)) == Modifiers.Control;

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

    private async void OnPromptSubmittedAsync(object? sender, SubmittedEventArgs e)
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

        if (userText.StartsWith('/'))
        {
            ExecuteSlashCommand(userText);
            return;
        }

        AppendEntry(new ChatEntry(ChatEntryKind.User, "You", Text.Escape(userText)));
        SetBusy(true);
        _turnCancellation = new CancellationTokenSource();

        try
        {
            _conversation ??= CreateConversation();
            var result = await _conversation.SendAsync(userText, _turnCancellation.Token).ConfigureAwait(true);
            foreach (var conversationEvent in result.Events)
            {
                AppendEvent(conversationEvent);
            }
        }
        catch (OperationCanceledException)
        {
            AppendEntry(new ChatEntry(ChatEntryKind.System, "Cancelled", "Interrupted by Escape. The session continues."));
        }
        catch (Exception exception)
        {
            AppendEntry(new ChatEntry(ChatEntryKind.Error, "Error", Text.Escape(exception.Message)));
        }
        finally
        {
            _turnCancellation.Dispose();
            _turnCancellation = null;
            SetBusy(false);
        }
    }

    private void ExecuteSlashCommand(string commandLine)
    {
        var name = commandLine.Split(' ', 2)[0];
        switch (name.ToLowerInvariant())
        {
            case "/help":
                AppendEntry(new ChatEntry(ChatEntryKind.System, "Commands", string.Join(
                    "\n",
                    SlashCommands.All.Select(static command => $"- `{command.Name}` — {command.Description}"))));
                break;
            case "/clear":
                _entries.Clear();
                RefreshTranscript();
                AppendEntry(new ChatEntry(ChatEntryKind.System, "Cleared", "Transcript cleared. The session continues."));
                break;
            case "/new":
                (_conversation as IDisposable)?.Dispose();
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
            case "/model":
                AppendEntry(new ChatEntry(ChatEntryKind.System, "Model", $"`{OpenAiEnvironment.ModelId()}`"));
                break;
            case "/workspace":
                AppendEntry(new ChatEntry(ChatEntryKind.System, "Workspace", $"`{Text.Escape(_workspaceRoot)}`"));
                break;
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

    private IConversationSession CreateConversation()
    {
        _status.Content = "Starting AgentKit runtime...";
        return AgentRuntime.Create(_workspaceRoot, OpenAiEnvironment.RequireApiKey(), OpenAiEnvironment.ModelId(), this);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This is called from deep inside the running turn's own call stack (the agent loop's tool-call loop),
    /// never from the UI-thread continuation <see cref="OnPromptSubmittedAsync"/> resumes on, because AgentKit's
    /// internal awaits use <c>ConfigureAwait(false)</c>. Every UI mutation here is therefore explicitly marshaled
    /// through <see cref="ControlBase.Dispatcher"/> rather than assumed to already run on it.
    /// </remarks>
    public async Task<bool> ConfirmAsync(string toolName, string argumentsJson, CancellationToken cancellationToken)
    {
        var approval = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var previousStatus = "";
        await Dispatcher!.InvokeAsync(
            () =>
            {
                AppendEntry(new ChatEntry(
                    ChatEntryKind.System,
                    $"⚠ Approve {Text.Escape(toolName)}?",
                    SourceLanguage.PrettyPrint(argumentsJson),
                    IsCode: true,
                    Language: SourceLanguage.Json));
                AppendEntry(new ChatEntry(
                    ChatEntryKind.System,
                    "Your decision",
                    "Press **Enter**/**y** to allow, **Esc**/**n** to deny."));
                previousStatus = _status.Content;
                _status.Content = $"Approve {toolName}? (Enter/y = allow, Esc/n = deny)";
                _pendingApproval = approval;
            },
            cancellationToken).ConfigureAwait(false);

        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<bool>) state!).TrySetResult(false), approval);
        var approved = await approval.Task.ConfigureAwait(false);

        await Dispatcher!.InvokeAsync(
            () =>
            {
                AppendEntry(new ChatEntry(
                    approved ? ChatEntryKind.ToolResultSuccess : ChatEntryKind.ToolResultFailure,
                    approved ? "Approved" : "Denied",
                    approved ? "The action will run." : "The action was skipped."));
                _pendingApproval = null;
                _status.Content = previousStatus;
            },
            CancellationToken.None).ConfigureAwait(false);
        return approved;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _prompt.IsReadOnly = busy;
        _spinner.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _status.Content = busy ? "Thinking... (Esc to cancel)" : "Ready";
    }

    private void AppendEvent(ConversationEvent conversationEvent)
    {
        switch (conversationEvent)
        {
            case ConversationAssistantTextEvent text:
                AppendEntry(new ChatEntry(ChatEntryKind.Assistant, "Assistant", text.Text));
                break;
            case ConversationToolCallEvent call:
                if (!_pendingToolArguments.TryGetValue(call.ToolName, out var callQueue))
                {
                    callQueue = new Queue<string>();
                    _pendingToolArguments.Add(call.ToolName, callQueue);
                }

                callQueue.Enqueue(call.ArgumentsJson);
                AppendEntry(new ChatEntry(
                    ChatEntryKind.ToolCall,
                    $"→ {Text.Escape(call.ToolName)}",
                    SourceLanguage.PrettyPrint(call.ArgumentsJson),
                    IsCode: true,
                    Language: SourceLanguage.Json));
                break;
            case ConversationToolResultEvent result:
                var arguments = _pendingToolArguments.TryGetValue(result.ToolName, out var resultQueue)
                    && resultQueue.TryDequeue(out var dequeued)
                        ? dequeued
                        : null;
                AppendEntry(BuildToolResultEntry(result, arguments));
                if (result.Succeeded && result.ToolName is "todo" or "plan" && TodoState.TryParse(result.Summary, out var title, out var items))
                {
                    _todoTitle = title;
                    _todoItems = items;
                    RefreshSidebarTodo();
                }

                break;
            case ConversationUsageEvent usage:
                _usage.Add(usage.Usage);
                RefreshSidebarContext();
                break;
            default:
                break;
        }
    }

    /// <summary>Every JSON-returning tool other than <c>read_file</c> — its result is source/data text that
    /// must keep its own line structure, not prose, but it's JSON rather than the file content a path/language
    /// lookup would apply to.</summary>
    private static readonly FrozenSet<string> _jsonResultTools =
        FrozenSet.ToFrozenSet(["edit", "glob", "search", "todo", "plan", "command"]);

    private static ChatEntry BuildToolResultEntry(ConversationToolResultEvent result, string? argumentsJson)
    {
        var header = (result.Succeeded ? "✓ " : "✗ ") + Text.Escape(result.ToolName);
        if (!result.Succeeded)
        {
            return new ChatEntry(ChatEntryKind.ToolResultFailure, header, Text.Escape(Truncate(result.Summary, 2_000)));
        }

        if (result.ToolName == "read_file")
        {
            var path = TryGetArgumentString(argumentsJson, "path");
            return new ChatEntry(
                ChatEntryKind.ToolResultSuccess, header, Truncate(result.Summary, 8_000), IsCode: true, Language: SourceLanguage.ForPath(path));
        }

        return _jsonResultTools.Contains(result.ToolName)
            ? new ChatEntry(
                ChatEntryKind.ToolResultSuccess,
                header,
                Truncate(SourceLanguage.PrettyPrint(result.Summary), 8_000),
                IsCode: true,
                Language: SourceLanguage.Json)
            : new ChatEntry(ChatEntryKind.ToolResultSuccess, header, Truncate(result.Summary, 8_000), IsCode: true);
    }

    private static string? TryGetArgumentString(string? argumentsJson, string propertyName)
    {
        if (argumentsJson is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            return document.RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void RefreshSidebarContext()
    {
        var summary = _usage.Format();
        _sidebarContext.Content = summary is null ? "<d>No usage reported yet.</d>" : $"<d>{summary}</d>";
        _usageStatus.Content = summary is null ? "<d>–</d>" : $"<d>{Text.Escape(summary)}</d>";
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

    private void RefreshTranscript()
    {
        _transcript.Items = [.. _entries];
        if (_entries.Count > 0)
        {
            _ = _transcript.BringIntoView(_entries.Count - 1);
        }
    }

    private Stack BuildEntryView(object? item)
    {
        var entry = (ChatEntry) item!;
        var tag = MarkupTagFor(entry.Kind);
        var header = new Text($"<{tag}><b>{Text.Escape(entry.HeaderText)}</b></{tag}>")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var body = BuildEntryBody(entry);

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
            Children = { header, body },
        };
    }

    /// <remarks>
    /// Markdown prose joins adjacent non-blank lines into one paragraph unless every line ends with a hard
    /// break, which is exactly wrong for source, JSON, or command output: <see cref="Document"/> is only used
    /// for genuine prose (assistant/user/system text). Everything the model gets back from a tool
    /// (<see cref="ChatEntry.IsCode"/>) instead goes through <see cref="CodeView"/>, which preserves line
    /// structure unconditionally and adds real per-token syntax color for the languages
    /// <see cref="SourceLanguage"/> maps.
    /// </remarks>
    private ControlBase BuildEntryBody(ChatEntry entry)
    {
        if (!entry.IsCode)
        {
            var document = new Document { HorizontalAlignment = HorizontalAlignment.Stretch };
            _ = document.Load(entry.Body, _markdownReader);
            return document;
        }

        return new CodeView
        {
            Code = entry.Body,
            Language = entry.Language,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Wrap,
            IsFoldingEnabled = false,
            // A per-row transcript control must never take focus itself, for the same reason the transcript
            // and command-popup ListViews below must not: see their own remarks for the SharpVision gap this
            // works around (a focusable control anywhere in the tree can silently steal focus and, with it,
            // arrow-key routing, from the always-focused prompt).
            IsFocusable = false,
            IsTabStop = false,
        };
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

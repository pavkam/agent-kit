// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Edits the model, reasoning effort, and turn limit used to compose the next conversation.</summary>
/// <remarks>
/// Completes with the edited configuration, or null when dismissed. Permissions are deliberately
/// not here: the live permission mode belongs to the Permissions menu, the palette, and
/// <c>/permissions</c>, which change it without restarting the session, whereas everything in this
/// dialog starts a fresh session on save. Reasoning effort is a five-way choice, so it is a radio
/// group rather than a slider: every option is visible and one key or click selects it. The turn
/// limit is a true range and keeps a slider, with a live readout and labeled endpoints.
/// </remarks>
internal sealed class AgentConfigurationDialog: CodingAgentDialog<CodingAgentConfiguration?>
{
    /// <summary>The smallest tool-calling turn limit the dialog offers.</summary>
    internal const int MinimumTurns = 4;

    /// <summary>The largest tool-calling turn limit the dialog offers.</summary>
    internal const int MaximumTurns = 40;

    private readonly Text _modelDescription = new() { Overflow = Overflow.Wrap, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Text _reasoningDescription = new() { Overflow = Overflow.Wrap, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Text _turnsValue = new() { TextAlignment = Alignment.End };
    private readonly Text _turnsDescription = new() { Overflow = Overflow.Wrap, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ImmutableArray<string> _readOnlyRoots;

    /// <summary>Initializes the dialog with the current pending configuration loaded.</summary>
    /// <param name="configuration">The non-null current pending runtime configuration.</param>
    /// <param name="isBusy">Whether an admitted turn is running, which locks saving until it ends.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    public AgentConfigurationDialog(CodingAgentConfiguration configuration, bool isBusy)
        : base("Agent Configuration", cancelledResult: null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _readOnlyRoots = configuration.ReadOnlyToolchainRoots;

        Width = Length.Percent(88);
        MaxWidth = Length.Cells(80);
        Height = Length.Auto;
        MaxHeight = Length.Percent(92);

        ModelSelector.Items = [.. CodingAgentConfiguration.Models.Cast<object>()];
        ModelSelector.TextSelector = static item => item is CodingAgentModelOption option ? option.ToString() : "";
        ModelSelector.HorizontalAlignment = HorizontalAlignment.Stretch;
        ModelSelector.AllowNull = false;
        ModelSelector.SelectedIndex = Math.Max(0, IndexOfModel(configuration.ModelId));
        ModelSelector.SelectionChanged += (_, _) => RefreshModelDescription();

        ReasoningChoices =
        [
            .. CodingAgentConfiguration.ReasoningEfforts.Select(effort => new RadioButton(ReasoningChoiceLabel(effort))
            {
                GroupName = "reasoning-effort",
                IsChecked = effort == configuration.ReasoningEffort,
            }),
        ];
        foreach (var choice in ReasoningChoices)
        {
            choice.Checked += (_, _) => RefreshReasoningDescription();
        }

        TurnSlider.Value = Math.Clamp(configuration.MaximumTurns, MinimumTurns, MaximumTurns);
        TurnSlider.ValueChanged += (_, _) => RefreshTurnReadout();

        RefreshModelDescription();
        RefreshReasoningDescription();
        RefreshTurnReadout();

        var status = Note(isBusy
            ? "<warning><b>●</b></warning> <b>Running</b> <d>· settings unlock after the active turn</d>"
            : "<success><b>●</b></success> <b>Ready</b> <d>· saving starts a fresh session</d>");
        status.Margin = new Thickness(0, 0, 0, 1);

        var model = new Stack
        {
            Orientation = Orientation.Vertical,
            Children = { ModelSelector, _modelDescription },
        };

        var reasoningRow = new Wrap { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var choice in ReasoningChoices)
        {
            reasoningRow.Children.Add(choice);
        }

        var reasoning = new Stack
        {
            Orientation = Orientation.Vertical,
            Children = { reasoningRow, _reasoningDescription },
        };

        var turnsHeader = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        turnsHeader.Columns.Add(Track.Star(1));
        turnsHeader.Columns.Add(Track.Auto());
        var turnsCaption = new Text("<d>Tool calls the agent may chain in one run</d>")
        {
            Overflow = Overflow.Ellipsis,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Grid.SetColumn(_turnsValue, 1);
        turnsHeader.Children.Add(turnsCaption);
        turnsHeader.Children.Add(_turnsValue);

        var turnsScale = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        turnsScale.Columns.Add(Track.Star(1));
        turnsScale.Columns.Add(Track.Star(1));
        var scaleMinimum = new Text($"<d>{MinimumTurns} · quick fix</d>");
        var scaleMaximum = new Text($"<d>{MaximumTurns} · long refactor</d>") { TextAlignment = Alignment.End };
        Grid.SetColumn(scaleMaximum, 1);
        turnsScale.Children.Add(scaleMinimum);
        turnsScale.Children.Add(scaleMaximum);

        var turns = new Stack
        {
            Orientation = Orientation.Vertical,
            Children = { turnsHeader, TurnSlider, turnsScale, _turnsDescription },
        };

        var save = new Button("&Save") { IsDefault = true, IsEnabled = !isBusy };
        var cancel = new Button("&Cancel") { IsCancel = true };
        save.Click += (_, _) => Complete(Edited());
        cancel.Click += (_, _) => Cancel();

        var body = new Stack
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            IsEnabled = !isBusy,
            Children =
            {
                status,
                Section("Model", model),
                Section("Reasoning effort", reasoning),
                Section("Turn limit", turns),
            },
        };
        Content = Compose(body, [save, cancel], hint: "Tab moves · ←/→ adjust · Space selects");
    }

    /// <summary>Gets the native model selector for focused verification.</summary>
    internal ComboBox ModelSelector { get; } = new();

    /// <summary>Gets the reasoning-effort radio buttons, in <see cref="CodingAgentConfiguration.ReasoningEfforts"/> order.</summary>
    internal ImmutableArray<RadioButton> ReasoningChoices { get; }

    /// <summary>Gets the native turn-limit slider for focused verification.</summary>
    internal Slider TurnSlider { get; } = new()
    {
        Minimum = MinimumTurns,
        Maximum = MaximumTurns,
        SmallChange = 1,
        LargeChange = 4,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    /// <summary>Gets the reasoning effort currently selected in the radio group.</summary>
    internal LlmReasoningEffort SelectedReasoningEffort
    {
        get
        {
            for (var index = 0; index < ReasoningChoices.Length; index++)
            {
                if (ReasoningChoices[index].IsChecked)
                {
                    return CodingAgentConfiguration.ReasoningEfforts[index];
                }
            }

            return CodingAgentConfiguration.ReasoningEfforts[0];
        }
    }

    /// <summary>Presents a fresh dialog and completes with the saved configuration or null.</summary>
    /// <param name="owner">The attached control whose presentation host shows the dialog.</param>
    /// <param name="configuration">The non-null current pending runtime configuration.</param>
    /// <param name="isBusy">Whether an admitted turn is running.</param>
    /// <param name="cancellationToken">Cancels the presentation, completing with null.</param>
    /// <returns>The edited configuration, or null when dismissed.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is detached.</exception>
    public static Task<CodingAgentConfiguration?> ShowAsync(
        ControlBase owner,
        CodingAgentConfiguration configuration,
        bool isBusy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var dialog = new AgentConfigurationDialog(configuration, isBusy);
        return dialog.PresentAsync(owner, isBusy ? null : dialog.ModelSelector, cancellationToken);
    }

    /// <summary>Maps a portable reasoning setting to concise product language.</summary>
    /// <param name="effort">The defined effort to describe.</param>
    /// <returns>A readable label and workload hint.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="effort"/> is unknown.</exception>
    internal static string ReasoningLabel(LlmReasoningEffort effort) => effort switch
    {
        LlmReasoningEffort.None => "Off · enables tools on Chat Completions",
        LlmReasoningEffort.Low => "Low · quick changes",
        LlmReasoningEffort.Medium => "Medium · balanced",
        LlmReasoningEffort.High => "High · complex debugging",
        LlmReasoningEffort.ExtraHigh => "Extra high · hardest work",
        _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "The reasoning effort is unknown."),
    };

    /// <summary>Builds the short mnemonic-bearing caption for one reasoning radio button.</summary>
    /// <param name="effort">The defined effort to caption.</param>
    /// <returns>A caption whose access key is unique within the group.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="effort"/> is unknown.</exception>
    internal static string ReasoningChoiceLabel(LlmReasoningEffort effort) => effort switch
    {
        LlmReasoningEffort.None => "&Off",
        LlmReasoningEffort.Low => "&Low",
        LlmReasoningEffort.Medium => "&Medium",
        LlmReasoningEffort.High => "&High",
        LlmReasoningEffort.ExtraHigh => "E&xtra high",
        _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "The reasoning effort is unknown."),
    };

    /// <summary>Describes what one turn limit means for the agent's working style.</summary>
    /// <param name="turns">The turn limit inside the dialog's range.</param>
    /// <returns>A one-line explanation of the expected run shape.</returns>
    internal static string TurnLimitDescription(int turns) => turns switch
    {
        <= 8 => "Short runs: a few reads and one edit before the agent reports back.",
        <= 16 => "Balanced: enough for a read, edit, test, and fix cycle.",
        <= 28 => "Long runs: multi-file changes with several test rounds.",
        _ => "Very long runs: large refactors; watch usage in the sidebar.",
    };

    private CodingAgentConfiguration Edited()
    {
        var model = (CodingAgentModelOption) ModelSelector.SelectedItem!;
        return new CodingAgentConfiguration(
            model.ModelId,
            SelectedReasoningEffort,
            TurnSlider.Value,
            _readOnlyRoots);
    }

    private void RefreshModelDescription() =>
        _modelDescription.Content = ModelSelector.SelectedItem is CodingAgentModelOption option
            ? $"<d>{Text.Escape(option.Description)}</d>"
            : "<d>Choose the OpenAI model that answers every request.</d>";

    private void RefreshReasoningDescription() =>
        _reasoningDescription.Content = $"<d>{Text.Escape(ReasoningLabel(SelectedReasoningEffort))}</d>";

    private void RefreshTurnReadout()
    {
        _turnsValue.Content = $"<accent><b>{TurnSlider.Value}</b></accent> <d>turns</d>";
        _turnsDescription.Content = $"<d>{Text.Escape(TurnLimitDescription(TurnSlider.Value))}</d>";
    }

    private static int IndexOfModel(string modelId) =>
        Array.FindIndex(CodingAgentConfiguration.Models.ToArray(), option =>
            string.Equals(option.ModelId, modelId, StringComparison.Ordinal));
}

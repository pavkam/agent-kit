// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Edits the model, reasoning effort, and turn limit used to compose the next conversation.</summary>
/// <remarks>
/// Completes with the edited configuration, or null when dismissed. Permissions are deliberately
/// not here: the live permission mode belongs to the Permissions menu, the palette, and
/// <c>/permissions</c>, which change it without restarting the session, whereas everything in this
/// dialog starts a fresh session on save.
/// </remarks>
internal sealed class AgentConfigurationDialog: CodingAgentDialog<CodingAgentConfiguration?>
{
    private readonly Text _reasoningValue = new();
    private readonly Slider _maximumTurns = new()
    {
        Minimum = 4,
        Maximum = 24,
        SmallChange = 1,
        LargeChange = 4,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
    private readonly Text _maximumTurnsValue = new();
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
        MaxWidth = Length.Cells(78);
        Height = Length.Auto;
        MaxHeight = Length.Percent(90);

        ModelSelector.Items = [.. CodingAgentConfiguration.Models.Cast<object>()];
        ModelSelector.TextSelector = static item => item is CodingAgentModelOption option ? option.ToString() : "";
        ModelSelector.HorizontalAlignment = HorizontalAlignment.Stretch;
        ModelSelector.SelectedIndex = Math.Max(0, IndexOfModel(configuration.ModelId));
        ReasoningSlider.Value = CodingAgentConfiguration.ReasoningEfforts.IndexOf(configuration.ReasoningEffort);
        _maximumTurns.Value = configuration.MaximumTurns;
        ReasoningSlider.ValueChanged += (_, _) => RefreshReasoningLabel();
        _maximumTurns.ValueChanged += (_, _) => RefreshTurnLabel();
        RefreshReasoningLabel();
        RefreshTurnLabel();

        var status = new Text(isBusy
            ? "<warning><b>Running</b></warning> · settings unlock after the active turn"
            : "<success><b>Ready</b></success> · saving starts a fresh session");
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
                new Text(""),
                new Text("<accent><b>OpenAI model</b></accent>"),
                ModelSelector,
                new Text("<accent><b>Reasoning effort</b></accent>"),
                ReasoningSlider,
                _reasoningValue,
                new Text("<accent><b>Tool-call turn limit</b></accent>"),
                _maximumTurns,
                _maximumTurnsValue,
            },
        };
        Content = Compose(body, [save, cancel]);
    }

    /// <summary>Gets the native model selector for focused verification.</summary>
    internal ComboBox ModelSelector { get; } = new();

    /// <summary>Gets the native reasoning slider for focused verification.</summary>
    internal Slider ReasoningSlider { get; } = new()
    {
        Minimum = 0,
        Maximum = 4,
        SmallChange = 1,
        LargeChange = 1,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

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

    private CodingAgentConfiguration Edited()
    {
        var model = (CodingAgentModelOption) ModelSelector.SelectedItem!;
        return new CodingAgentConfiguration(
            model.ModelId,
            CodingAgentConfiguration.ReasoningEfforts[ReasoningSlider.Value],
            _maximumTurns.Value,
            _readOnlyRoots);
    }

    private void RefreshReasoningLabel() =>
        _reasoningValue.Content = $"<d>{ReasoningLabel(CodingAgentConfiguration.ReasoningEfforts[ReasoningSlider.Value])}</d>";

    private void RefreshTurnLabel() =>
        _maximumTurnsValue.Content = $"<d>{_maximumTurns.Value} turns per run</d>";

    private static int IndexOfModel(string modelId) =>
        Array.FindIndex(CodingAgentConfiguration.Models.ToArray(), option =>
            string.Equals(option.ModelId, modelId, StringComparison.Ordinal));
}

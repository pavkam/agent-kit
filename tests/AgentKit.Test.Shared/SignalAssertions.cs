// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Globalization;

/// <summary>Asserts that diagnostic signals do not contain protected literal content.</summary>
public static class SignalAssertions
{
    /// <summary>Asserts that no supplied forbidden literal appears in activities, logs, or metric tags.</summary>
    /// <param name="activities">Terminal activities to scan.</param>
    /// <param name="logs">Structured log entries to scan.</param>
    /// <param name="metrics">Metric observations to scan.</param>
    /// <param name="forbidden">One or more literals that must not appear anywhere in the signals.</param>
    /// <exception cref="ArgumentNullException">A collection argument is null.</exception>
    /// <exception cref="ShouldAssertException">A forbidden literal was found.</exception>
    public static void ShouldNotContainContent(
        IEnumerable<ActivityObservation> activities,
        IEnumerable<RecordingLogEntry> logs,
        IEnumerable<MetricObservation> metrics,
        params string[] forbidden)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(logs);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(forbidden);
        foreach (var literal in forbidden)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(literal, nameof(forbidden));
            foreach (var activity in activities)
            {
                ContainsLiteral(activity.OperationName, literal).ShouldBe(false, $"activity operation name leaked '{literal}'");
                foreach (var tag in activity.Tags.Values)
                {
                    ContainsLiteral(tag, literal).ShouldBe(false, $"activity tag leaked '{literal}'");
                }
            }

            foreach (var log in logs)
            {
                ContainsLiteral(log.Message, literal).ShouldBe(false, $"log message leaked '{literal}'");
                foreach (var field in log.State.Values)
                {
                    ContainsLiteral(field, literal).ShouldBe(false, $"log field leaked '{literal}'");
                }
            }

            foreach (var metric in metrics)
            {
                ContainsLiteral(metric.InstrumentName, literal).ShouldBe(false, $"metric name leaked '{literal}'");
                foreach (var tag in metric.Tags.Values)
                {
                    ContainsLiteral(tag, literal).ShouldBe(false, $"metric tag leaked '{literal}'");
                }
            }
        }
    }

    private static bool ContainsLiteral(object? value, string literal)
    {
        return value is not null && value switch
        {
            string text => text.Contains(literal, StringComparison.Ordinal),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)
                .Contains(literal, StringComparison.Ordinal),
            _ => value.ToString()?.Contains(literal, StringComparison.Ordinal) == true,
        };
    }
}

// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using System.Text.Json;

/// <summary>One item of the session's current todo/work plan, as last reported by the todo tool.</summary>
internal enum TodoStatus
{
    Pending,
    InProgress,
    Completed,
    Blocked,
}

/// <summary>One parsed todo/work-plan item.</summary>
internal sealed record TodoItem(string Id, string Text, TodoStatus Status);

/// <summary>Parses the todo/plan tool's own result JSON shape (<c>{"plan":{"title":...,"items":[...]}}</c>)
/// into a display-friendly snapshot, without going through the tool's own protected read path — the sidebar only
/// ever needs what the model itself already saw in the tool result.</summary>
internal static class TodoState
{
    /// <summary>Attempts to parse one todo/plan tool result into its title and items.</summary>
    /// <param name="resultJson">The tool result's raw JSON text.</param>
    /// <param name="title">The plan's title, or null when absent or the plan does not exist.</param>
    /// <param name="items">The parsed items, empty when the plan does not exist or has none.</param>
    /// <returns>Whether <paramref name="resultJson"/> parsed as valid JSON in the expected shape.</returns>
    public static bool TryParse(string resultJson, out string? title, out ImmutableArray<TodoItem> items)
    {
        title = null;
        items = [];

        try
        {
            using var document = JsonDocument.Parse(resultJson);
            if (!document.RootElement.TryGetProperty("plan", out var planElement))
            {
                return false;
            }

            if (planElement.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            title = planElement.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
            if (!planElement.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
            {
                return true;
            }

            var builder = ImmutableArray.CreateBuilder<TodoItem>();
            foreach (var item in itemsElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("id", out var idElement) ||
                    !item.TryGetProperty("text", out var textElement) ||
                    !item.TryGetProperty("status", out var statusElement) ||
                    idElement.GetString() is not { } id ||
                    textElement.GetString() is not { } text ||
                    !TryParseStatus(statusElement.GetString(), out var status))
                {
                    continue;
                }

                builder.Add(new TodoItem(id, text, status));
            }

            items = builder.ToImmutable();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseStatus(string? text, out TodoStatus status)
    {
        switch (text)
        {
            case "pending":
                status = TodoStatus.Pending;
                return true;
            case "in_progress":
                status = TodoStatus.InProgress;
                return true;
            case "completed":
                status = TodoStatus.Completed;
                return true;
            case "blocked":
                status = TodoStatus.Blocked;
                return true;
            default:
                status = default;
                return false;
        }
    }
}

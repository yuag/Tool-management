using QuickLaunch.Core.Models;

namespace QuickLaunch.Core.Services;

/// <summary>Filters launch items by a simple case-insensitive query.</summary>
public sealed class SearchService
{
    public IReadOnlyList<LaunchItem> Filter(AppConfig config, string? query)
    {
        var all = config.Groups.SelectMany(g => g.Items).ToList();
        if (string.IsNullOrWhiteSpace(query))
        {
            return all;
        }

        var q = query.Trim();
        return all
            .Where(i =>
                i.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Path.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Type.ToString().Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

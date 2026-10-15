using System;

namespace Fixture.Structure;

/// <summary>A realistic service used to check that signature extraction keeps interfaces intact.</summary>
public interface IWidgetService
{
    int Count { get; }

    Widget? Find(string id);
}

/// <summary>The default implementation.</summary>
public sealed class WidgetService : IWidgetService
{
    private readonly Dictionary<string, Widget> _widgets = new();

    public WidgetService(IEnumerable<Widget> widgets)
    {
        foreach (var widget in widgets)
        {
            _widgets[widget.Id] = widget;
        }
    }

    public int Count => _widgets.Count;

    public Widget? Find(string id)
    {
        if (!_widgets.TryGetValue(id, out var widget))
        {
            return null;
        }

        return widget;
    }

    public string Describe<T>(T value)
        where T : notnull
        => value.ToString() ?? string.Empty;
}

public sealed record Widget(string Id, string Name)
{
    public string Label
    {
        get { return $"{Name} ({Id})"; }
    }
}

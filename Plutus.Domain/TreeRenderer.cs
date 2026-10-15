namespace Plutus.Domain;

public class TreeRenderer
{
    public List<string> RenderTree(ProjectNode rootNode)
    {
        var rootLabel = rootNode.IsDirectory ? rootNode.Name : rootNode.Name + GetNodeMarker(rootNode);
        var lines = new List<string> { rootLabel };
        WalkNodes(rootNode, "", lines);
        return lines;
    }

    private void WalkNodes(ProjectNode parent, string prefix, List<string> lines)
    {
        for (var i = 0; i < parent.Children.Count; i++)
        {
            var child = parent.Children[i];
            var isLast = i == parent.Children.Count - 1;
            var branch = isLast ? "└── " : "├── ";
            var nextPrefix = prefix + (isLast ? "    " : "│   ");

            lines.Add($"{prefix}{branch}{child.Name}{GetNodeMarker(child)}");

            // An excluded directory is still worth descending into when a change beneath it carries a limit
            // marker: that marker is the only place the omitted change is reported.
            if (child.IsDirectory && (child.Status == NodeStatus.Included || ContainsLimitMarker(child)))
            {
                WalkNodes(child, nextPrefix, lines);
            }
        }
    }

    private static bool ContainsLimitMarker(ProjectNode node)
    {
        foreach (var child in node.Children)
        {
            if (!child.IsDirectory && child.Status is NodeStatus.OversizedFile or NodeStatus.TokenBudgetOmitted)
            {
                return true;
            }

            if (child.IsDirectory && ContainsLimitMarker(child))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetNodeMarker(ProjectNode node) => node.Status switch
    {
        NodeStatus.IgnoredDirectory => " [IGNORED DIR]/",
        NodeStatus.IgnoredFile => " [IGNORED FILE]",
        NodeStatus.OversizedFile => $" [LARGE {node.FileInfo!.Length.HumanSize()}]",
        NodeStatus.SkippedFile => " [SKIPPED]",
        NodeStatus.TokenBudgetOmitted => " [OMITTED BY BUDGET]",
        _ => node.IsDirectory ? "/" : $" [{node.FileInfo!.Length.HumanSize()}]"
    };
}

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

            if (child is { IsDirectory: true, Status: NodeStatus.Included })
            {
                WalkNodes(child, nextPrefix, lines);
            }
        }
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

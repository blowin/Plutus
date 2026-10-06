using Microsoft.Extensions.FileProviders;
using Plutus.Domain.IgnoreMatcher;

namespace Plutus.Domain;

public class TreeRenderer
{
    public List<string> RenderTree(ProjectNode rootNode)
    {
        var lines = new List<string> { rootNode.Name };
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

            var marker = child.Status switch
            {
                NodeStatus.IgnoredDirectory => " [IGNORED DIR]/",
                NodeStatus.IgnoredFile => " [IGNORED FILE]",
                NodeStatus.OversizedFile => $" [LARGE {child.FileInfo!.Length.HumanSize()}]",
                _ => child.IsDirectory ? "/" : $" [{child.FileInfo!.Length.HumanSize()}]"
            };

            lines.Add($"{prefix}{branch}{child.Name}{marker}");

            if (child.IsDirectory && child.Status == NodeStatus.Included)
            {
                WalkNodes(child, nextPrefix, lines);
            }
        }
    }
}


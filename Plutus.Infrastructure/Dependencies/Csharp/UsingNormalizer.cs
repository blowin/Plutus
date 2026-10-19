using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Plutus.Infrastructure.Dependencies.Csharp;

public class UsingNormalizer
{
    public string? Normalize(UsingDirectiveSyntax u)
    {
        if (u.Name == null)
        {
            return null;
        }

        // Extract the full target string text safely, avoiding Roslyn's node-slicing variations
        var fullUsingStr = u.ToString();
        string nameStr;

        if (u.Alias != null)
        {
            // For 'using Comp = MyApp.Components;', isolate everything between '=' and ';'
            var eqIndex = fullUsingStr.IndexOf('=');
            var semicolonIndex = fullUsingStr.IndexOf(';');
            if (eqIndex >= 0 && semicolonIndex > eqIndex)
            {
                nameStr = fullUsingStr[(eqIndex + 1)..semicolonIndex];
            }
            else
            {
                nameStr = u.Name.ToString();
            }
        }
        else
        {
            nameStr = u.Name.ToString();
        }

        // Clean trailing spaces or syntax keywords
        nameStr = nameStr.Trim();

        // For 'using static MyApp.Extensions.StringExtensions;', strip the static class name token
        if (!u.StaticKeyword.IsKind(SyntaxKind.None))
        {
            var lastDot = nameStr.LastIndexOf('.');
            if (lastDot > 0)
            {
                nameStr = nameStr[..lastDot];
            }
        }

        return nameStr.Trim();
    }
}

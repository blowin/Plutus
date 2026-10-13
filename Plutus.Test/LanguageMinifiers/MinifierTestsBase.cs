namespace Plutus.Test.LanguageMinifiers;

public class MinifierTestsBase
{
    public static string Normalize(string context) => context.Replace(Environment.NewLine, "\n");
}

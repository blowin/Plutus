namespace Plutus.Domain.IgnoreMatcher;

public class IgnoreMatcherFactory(IReadOnlyCollection<IIgnoreMatcher> additionalIgnoreMatchers)
{
    public IIgnoreMatcher CreateIgnoreMatcher(bool useDefaultExcludes, bool allowDangerousFiles)
    {
        var ignoreMatchBuilder = new IgnoreMatcherBuilder();
        foreach (IIgnoreMatcher additionalIgnoreMatcher in additionalIgnoreMatchers)
        {
            ignoreMatchBuilder.Add(additionalIgnoreMatcher);
        }

        if (useDefaultExcludes)
        {
            ignoreMatchBuilder.IgnoreJunk();
        }

        if (!allowDangerousFiles)
        {
            ignoreMatchBuilder.IgnoreDangerousFile();
        }

        return ignoreMatchBuilder.Build();
    }
}

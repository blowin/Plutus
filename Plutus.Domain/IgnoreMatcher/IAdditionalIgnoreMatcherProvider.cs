using Microsoft.Extensions.FileProviders;

namespace Plutus.Domain.IgnoreMatcher;

public interface IAdditionalIgnoreMatcherProvider
{
    IEnumerable<IIgnoreMatcher> GetMatchers(List<IFileProvider> roots, RunAppOptions options);
}

using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Structure;
using Plutus.Infrastructure.Structure;

namespace Plutus.Test;

/// <summary>
/// Layering rules the compiler cannot express: the domain owns the contracts of a transformation and stays free of
/// any parser, while the parser-backed implementations live in the infrastructure layer.
/// </summary>
public class ArchitectureTests
{
    [Fact]
    public void Domain_DoesNotReferenceAParser()
    {
        var referenced = typeof(LanguageId).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToList();

        referenced.Should().NotContain(
            name => name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal),
            "the domain describes transformations; parsing belongs to the infrastructure layer");
        referenced.Should().NotContain(
            name => name.StartsWith("Antlr4", StringComparison.Ordinal),
            "a grammar-backed implementation would live in the infrastructure layer as well");
    }

    [Fact]
    public void ParserBackedImplementations_LiveInInfrastructure()
    {
        typeof(ISignatureSkeletonProvider).Assembly.GetName().Name.Should().Be("Plutus.Domain");
        typeof(CSharpSignatureSkeletonProvider).Assembly.GetName().Name.Should().Be("Plutus.Infrastructure");
    }

    [Fact]
    public void Domain_DeclaresNoStructuralProvider()
    {
        typeof(ISignatureSkeletonProvider).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(ISignatureSkeletonProvider).IsAssignableFrom(type))
            .Should().BeEmpty("a provider needs a parser, and the domain must not have one");
    }

    [Fact]
    public void DomainAssembly_KeepsTheLanguageNeutralPartsOfTheStructuralPass()
    {
        // The contracts and the token rewriter stay in the domain: they are what an implementation is written
        // against, whatever parser produces the tokens.
        typeof(ISignatureSkeletonProvider).Assembly.Should().BeSameAs(typeof(TokenStreamRewriter).Assembly);
        typeof(ISignatureSkeletonProvider).Assembly.Should().BeSameAs(typeof(TokenSpan).Assembly);
        typeof(ISignatureSkeletonProvider).Assembly.Should().BeSameAs(typeof(SignatureExtractionOptions).Assembly);
    }
}

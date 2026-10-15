using FluentAssertions;
using Plutus.Domain;
using Plutus.Domain.Structure;
using Plutus.Infrastructure.Structure;

namespace Plutus.Test.Structure;

/// <summary>
/// Signature extraction for C#: what must survive (interfaces, abstract members, generics with constraints,
/// attributes, documentation comments, field initializers) and what must be cut (method, constructor, operator,
/// accessor and expression bodies).
/// </summary>
public class CSharpSignatureSkeletonProviderTests
{
    private static readonly CSharpSignatureSkeletonProvider Provider = new();

    [Fact]
    public void MethodBody_IsReplacedWithTheStructuralTerminator()
    {
        var source = """
            public class Sample
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }
            """;

        var expected = """
            public class Sample
            {
                public int Add(int a, int b);
            }
            """;

        var result = Extract(source);

        result.Content.Should().Be(expected);
        result.CountOf(MemberKind.Method).Should().Be(1);
        result.Provider.Should().Be("roslyn-csharp");
    }

    [Fact]
    public void InterfaceMembers_AndAbstractMembers_ArePreserved()
    {
        var source = """
            public interface IShape
            {
                double Area { get; }

                void Draw();
            }

            public abstract class Shape : IShape
            {
                public abstract double Area { get; }

                public abstract void Draw();

                public virtual string Describe() => "shape";
            }
            """;

        var result = Extract(source);

        // Every member except the single expression body is already a signature, so it survives untouched.
        result.CountOf(MemberKind.Method).Should().Be(1);
        result.CountOf(MemberKind.Property).Should().Be(0);
        result.Content.Should().Contain("double Area { get; }");
        result.Content.Should().Contain("void Draw();");
        result.Content.Should().Contain("public abstract double Area { get; }");
        result.Content.Should().Contain("public abstract void Draw();");
        result.Content.Should().Contain("public virtual string Describe();");
    }

    [Fact]
    public void GenericMethod_KeepsTypeParametersAndConstraints()
    {
        var source = """
            public class Mapper
            {
                public TOut Map<TIn, TOut>(TIn input, Func<TIn, TOut> selector)
                    where TIn : class
                    where TOut : new()
                {
                    return selector(input);
                }
            }
            """;

        var result = Extract(source);

        result.Content.Should().Contain("public TOut Map<TIn, TOut>(TIn input, Func<TIn, TOut> selector)");
        result.Content.Should().Contain("where TIn : class");
        result.Content.Should().Contain("where TOut : new();");
        result.Content.Should().NotContain("return selector(input);");
    }

    [Fact]
    public void ExpressionBodiedMethod_KeepsItsSignature()
    {
        var source = """
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
            }
            """;

        var result = Extract(source);

        result.Content.Should().Be("public class Calculator\n{\n    public int Add(int a, int b);\n}");
        result.CountOf(MemberKind.Method).Should().Be(1);
    }

    [Fact]
    public void ExpressionBodiedProperty_KeepsItsDeclaration()
    {
        var source = """
            public class Holder
            {
                private int _value;

                public int Value => _value;
            }
            """;

        var result = Extract(source);

        result.Content.Should().Contain("public int Value;");
        result.CountOf(MemberKind.Property).Should().Be(1);
        result.Content.Should().Contain("private int _value;");
    }

    [Fact]
    public void PropertyAccessorBodies_AreReplacedAndTheAccessorsStay()
    {
        var source = """
            public class Holder
            {
                private int _value;

                public int Value
                {
                    get { return _value; }
                    set { _value = value; }
                }
            }
            """;

        var result = Extract(source);

        result.Content.Should().Be("""
            public class Holder
            {
                private int _value;

                public int Value
                {
                    get;
                    set;
                }
            }
            """);
        result.CountOf(MemberKind.Accessor).Should().Be(2);
    }

    [Fact]
    public void AutoProperty_IsUntouched()
    {
        var source = """
            public class Holder
            {
                public int Value { get; set; }
            }
            """;

        var result = Extract(source);

        result.Content.Should().Be(source);
        result.StrippedBodyCount.Should().Be(0);
    }

    [Fact]
    public void ConstructorsDestructorsAndOperators_LoseTheirBodies()
    {
        var source = """
            public sealed class Value
            {
                public Value(int amount)
                {
                    Amount = amount;
                }

                ~Value()
                {
                    Dispose();
                }

                public static Value operator +(Value left, Value right) => new Value(left.Amount + right.Amount);

                public static explicit operator int(Value value) => value.Amount;

                public int Amount { get; }
            }
            """;

        var result = Extract(source);

        result.Content.Should().Contain("public Value(int amount);");
        result.Content.Should().Contain("~Value();");
        result.Content.Should().Contain("public static Value operator +(Value left, Value right);");
        result.Content.Should().Contain("public static explicit operator int(Value value);");
        result.CountOf(MemberKind.Constructor).Should().Be(1);
        result.CountOf(MemberKind.Destructor).Should().Be(1);
        result.CountOf(MemberKind.Operator).Should().Be(1);
        result.CountOf(MemberKind.ConversionOperator).Should().Be(1);
    }

    [Fact]
    public void NestedMemberInsideAStrippedBody_ProducesASingleEdit()
    {
        var source = """
            public class Outer
            {
                public void Run()
                {
                    int Local(int value) => value * 2;

                    Console.WriteLine(Local(2));
                }
            }
            """;

        var result = Extract(source);

        result.Content.Should().Be("public class Outer\n{\n    public void Run();\n}");
        result.StrippedBodyCount.Should().Be(1);
    }

    [Fact]
    public void AttributesDocumentationAndRegions_ArePreserved()
    {
        var source = """
            public class Documented
            {
                /// <summary>Adds two numbers.</summary>
                [Obsolete("kept")]
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }
            """;

        var result = Extract(source);

        result.Content.Should().Contain("/// <summary>Adds two numbers.</summary>");
        result.Content.Should().Contain("[Obsolete(\"kept\")]");
        result.Content.Should().Contain("public int Add(int a, int b);");
    }

    [Fact]
    public void FieldInitializers_AreKeptBecauseConstValuesArePartOfTheInterface()
    {
        var source = """
            public class Config
            {
                public const string Route = "https://example.test/api";

                private static readonly Regex Pattern = new("[/*]");

                public string Name { get; set; }
            }
            """;

        var result = Extract(source);

        result.Content.Should().Contain("public const string Route = \"https://example.test/api\";");
        result.Content.Should().Contain("private static readonly Regex Pattern = new(\"[/*]\");");
        result.StrippedBodyCount.Should().Be(0);
    }

    [Fact]
    public void RecordMembers_AreStrippedAndThePrimaryConstructorStays()
    {
        var source = """
            public sealed record Point(int X, int Y)
            {
                public double Length()
                {
                    return Math.Sqrt((X * X) + (Y * Y));
                }

                public string Label => $"{X},{Y}";
            }
            """;

        var result = Extract(source);

        result.Content.Should().Contain("public sealed record Point(int X, int Y)");
        result.Content.Should().Contain("public double Length();");
        result.Content.Should().Contain("public string Label;");
    }

    [Fact]
    public void EventAccessorBodies_AreReplaced()
    {
        var source = """
            public class Publisher
            {
                private EventHandler? _handlers;

                public event EventHandler Changed
                {
                    add { _handlers += value; }
                    remove { _handlers -= value; }
                }
            }
            """;

        var result = Extract(source);

        result.Content.Should().Contain("add;");
        result.Content.Should().Contain("remove;");
        result.CountOf(MemberKind.Accessor).Should().Be(2);
    }

    [Fact]
    public void StrippingIsIdempotent()
    {
        var source = """
            public class Mixed
            {
                public int Value { get; set; }

                public int Compute(int input) => input * 2;

                public void Run()
                {
                    Compute(2);
                }
            }
            """;

        var first = Extract(source);
        var second = Extract(first.Content);

        second.Content.Should().Be(first.Content);
        second.StrippedBodyCount.Should().Be(0);
    }

    [Fact]
    public void CrLfLineEndings_ArePreservedOutsideTheStrippedBodies()
    {
        const string source = "public class Sample\r\n{\r\n    public void M()\r\n    {\r\n        Work();\r\n    }\r\n}\r\n";

        var result = Extract(source);

        result.Content.Should().Be("public class Sample\r\n{\r\n    public void M();\r\n}\r\n");
    }

    [Fact]
    public void BrokenCode_StillProducesASkeletonAndReportsDiagnostics()
    {
        var source = """
            public class Broken
            {
                public void M()
                {
                    var x = ;
                }
            }
            """;

        var result = Extract(source);

        result.ParseDiagnostics.Should().BeGreaterThan(0);
        result.FallbackReason.Should().BeNull();
        result.Content.Should().Contain("public void M();");
    }

    [Fact]
    public void EmptySource_ProducesNoEdits()
    {
        var result = Extract(string.Empty);

        result.Content.Should().BeEmpty();
        result.StrippedBodyCount.Should().Be(0);
    }

    [Fact]
    public void FileWithoutBodies_IsReturnedUnchanged()
    {
        const string source = "namespace Sample;\n\npublic interface IThing\n{\n    void Do();\n}\n";

        var result = Extract(source);

        result.Content.Should().Be(source);
        result.ParseDiagnostics.Should().Be(0);
    }

    [Fact]
    public void SupportedMemberKinds_AreDeclaredByTheProvider()
    {
        Provider.SupportedMemberKinds.Should().Contain(MemberKind.Method);
        Provider.SupportedMemberKinds.Should().Contain(MemberKind.Property);
        Provider.SupportedMemberKinds.Should().Contain(MemberKind.Accessor);
        Provider.Language.Should().Be(LanguageId.CSharp);
    }

    private static SignatureExtractionResult Extract(string source) => Provider.Create(source, SignatureExtractionOptions.Default);
}

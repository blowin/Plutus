namespace Plutus.Domain.Structure;

/// <summary>What a stripped implementation body is replaced with.</summary>
public enum BodyTerminator
{
    /// <summary>
    /// A semicolon, the structural terminator a declaration without an implementation uses. This is the only
    /// supported terminator, and the enum keeps the choice explicit at every call site rather than a literal in the
    /// rewriter.
    /// </summary>
    Semicolon = 0,
}

public static class BodyTerminatorExtensions
{
    /// <summary>The text a stripped body is replaced with.</summary>
    public static string ToText(this BodyTerminator terminator) => terminator switch
    {
        BodyTerminator.Semicolon => ";",
        _ => ";",
    };
}

/// <summary>The kind of declaration an implementation body belonged to, for reporting and for tests.</summary>
public enum MemberKind
{
    Method,
    Constructor,
    Destructor,
    Operator,
    ConversionOperator,
    Property,
    Indexer,
    Event,
    Accessor,
    LocalFunction,
}

/// <summary>How a body was written, which decides how it is cut out.</summary>
public enum BodyKind
{
    /// <summary>A brace-delimited block, replaced as a whole.</summary>
    Block,

    /// <summary>An <c>=&gt;</c> expression body, replaced together with its terminating semicolon.</summary>
    ExpressionBody,
}

using Superpower.Display;

namespace UnrealAssetScout.TypeFiltering;

// Token kinds produced by TypeFilterTokenizer and consumed by the future expression parser.
internal enum TypeFilterToken
{
    [Token(Description = "identifier")]
    Identifier,

    [Token(Description = "number")]
    Number,

    [Token(Description = "keyword")]
    Keyword,

    [Token(Description = "and operator", Example = "and")]
    And,

    [Token(Description = "or operator", Example = "or")]
    Or,

    [Token(Description = "not operator", Example = "not")]
    Not,

    [Token(Example = "=")]
    Equal,

    [Token(Example = "!=")]
    NotEqual,

    [Token(Example = ">")]
    GreaterThan,

    [Token(Example = "<")]
    LessThan,

    [Token(Example = ">=")]
    GreaterThanOrEqual,

    [Token(Example = "<=")]
    LessThanOrEqual,

    [Token(Example = "(")]
    LeftParen,

    [Token(Example = ")")]
    RightParen
}

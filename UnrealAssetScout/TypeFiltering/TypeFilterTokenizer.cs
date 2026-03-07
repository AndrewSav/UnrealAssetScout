using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace UnrealAssetScout.TypeFiltering;

// Builds the Superpower tokenizer used for type-filter expressions.
// Used by TypeFilterParser when list/export runs evaluate expressions against cached type data.
internal static class TypeFilterTokenizer
{
    private static TextParser<Unit> IdentifierToken { get; } =
        from first in Character.Letter.Or(Character.EqualTo('_'))
        from rest in Character.LetterOrDigit
            .Or(Character.EqualTo('_'))
            .Or(Character.EqualTo('-'))
            .IgnoreMany()
        select Unit.Value;

    internal static Tokenizer<TypeFilterToken> Instance { get; } =
        new TokenizerBuilder<TypeFilterToken>()
            .Ignore(Span.WhiteSpace)
            .Match(Span.EqualTo("%t"), TypeFilterToken.Keyword, requireDelimiters: true)
            .Match(Span.EqualTo("%e"), TypeFilterToken.Keyword, requireDelimiters: true)
            .Match(Span.EqualTo("%types"), TypeFilterToken.Keyword, requireDelimiters: true)
            .Match(Span.EqualTo("%exports"), TypeFilterToken.Keyword, requireDelimiters: true)
            .Match(Span.EqualTo("&&"), TypeFilterToken.And)
            .Match(Span.EqualTo("||"), TypeFilterToken.Or)
            .Match(Span.EqualTo("!="), TypeFilterToken.NotEqual)
            .Match(Span.EqualTo(">="), TypeFilterToken.GreaterThanOrEqual)
            .Match(Span.EqualTo("<="), TypeFilterToken.LessThanOrEqual)
            .Match(Span.EqualTo("and"), TypeFilterToken.And, requireDelimiters: true)
            .Match(Span.EqualTo("or"), TypeFilterToken.Or, requireDelimiters: true)
            .Match(Span.EqualTo("not"), TypeFilterToken.Not, requireDelimiters: true)
            .Match(Character.EqualTo('&'), TypeFilterToken.And)
            .Match(Character.EqualTo('|'), TypeFilterToken.Or)
            .Match(Character.EqualTo('!'), TypeFilterToken.Not)
            .Match(Character.EqualTo('~'), TypeFilterToken.Not)
            .Match(Character.EqualTo('='), TypeFilterToken.Equal)
            .Match(Character.EqualTo('>'), TypeFilterToken.GreaterThan)
            .Match(Character.EqualTo('<'), TypeFilterToken.LessThan)
            .Match(Character.EqualTo('('), TypeFilterToken.LeftParen)
            .Match(Character.EqualTo(')'), TypeFilterToken.RightParen)
            .Match(Numerics.Natural, TypeFilterToken.Number, requireDelimiters: true)
            .Match(IdentifierToken, TypeFilterToken.Identifier, requireDelimiters: true)
            .Build();
}

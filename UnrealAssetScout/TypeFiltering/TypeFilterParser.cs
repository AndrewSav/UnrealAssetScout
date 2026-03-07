using System;
using System.Globalization;
using System.Linq;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;

namespace UnrealAssetScout.TypeFiltering;

// Parses type-filter expressions into predicates over PackageModel values.
// Called by TypeFilterSupport when list/export runs apply expression-based filtering from a
// previously generated `list --format types` CSV file.
internal sealed class TypeFilterParser
{
    private enum ValueKind
    {
        Number,
        Identifier,
        Keyword
    }

    private enum ComparisonOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual
    }

    private readonly record struct ValueOperand(ValueKind Kind, string RawValue, int Number);

    internal Func<PackageModel, bool> Parse(string expression)
    {
        var tokens = TypeFilterTokenizer.Instance.Tokenize(expression);
        return Parse(tokens);
    }

    internal Func<PackageModel, bool> Parse(TokenList<TypeFilterToken> tokens)
    {
        return Expression.AtEnd().Parse(tokens);
    }

    internal PackageModel[] Filter(System.Collections.Generic.IEnumerable<PackageModel> packages, string expression)
    {
        var predicate = Parse(expression);
        return packages.Where(predicate).ToArray();
    }

    private static TokenListParser<TypeFilterToken, Func<PackageModel, bool>> Expression =>
        OrExpression;

    private static TokenListParser<TypeFilterToken, Func<PackageModel, bool>> OrExpression =>
        from first in AndExpression
        from rest in (
            from _ in Token.EqualTo(TypeFilterToken.Or)
            from operand in AndExpression
            select operand).Many()
        select rest.Aggregate(
            first,
            static (left, right) => package => left(package) || right(package));

    private static TokenListParser<TypeFilterToken, Func<PackageModel, bool>> AndExpression =>
        from first in UnaryExpression
        from rest in (
            from _ in Token.EqualTo(TypeFilterToken.And)
            from operand in UnaryExpression
            select operand).Many()
        select rest.Aggregate(
            first,
            static (left, right) => package => left(package) && right(package));

    private static TokenListParser<TypeFilterToken, Func<PackageModel, bool>> UnaryExpression =>
        (
            from _ in Token.EqualTo(TypeFilterToken.Not)
            from operand in Superpower.Parse.Ref(() => UnaryExpression)
            select new Func<PackageModel, bool>(package => !operand(package)))
        .Or(Primary);

    private static TokenListParser<TypeFilterToken, Func<PackageModel, bool>> Primary =>
        ParenthesizedExpression
            .Or(NumberComparison)
            .Or(KeywordComparison)
            .Or(IdentifierExpression);

    private static TokenListParser<TypeFilterToken, Func<PackageModel, bool>> ParenthesizedExpression =>
        from _ in Token.EqualTo(TypeFilterToken.LeftParen)
        from expression in Superpower.Parse.Ref(() => Expression)
        from __ in Token.EqualTo(TypeFilterToken.RightParen)
        select expression;

    private static TokenListParser<TypeFilterToken, Func<PackageModel, bool>> IdentifierExpression =>
        from identifier in IdentifierValue
        from comparisonTail in ComparisonTail.Optional()
        select comparisonTail is null
            ? package => GetTypeCount(package, identifier) > 0
            : BuildComparisonPredicate(
                new ValueOperand(ValueKind.Identifier, identifier, 0),
                comparisonTail.Value.Operator,
                comparisonTail.Value.Right);

    private static TokenListParser<TypeFilterToken, Func<PackageModel, bool>> KeywordComparison =>
        from keyword in KeywordValue
        from comparisonTail in ComparisonTail
        select BuildComparisonPredicate(
            new ValueOperand(ValueKind.Keyword, keyword, 0),
            comparisonTail.Operator,
            comparisonTail.Right);

    private static TokenListParser<TypeFilterToken, Func<PackageModel, bool>> NumberComparison =>
        from number in NumberValue
        from comparisonTail in ComparisonTail
        select BuildComparisonPredicate(
            new ValueOperand(ValueKind.Number, string.Empty, number),
            comparisonTail.Operator,
            comparisonTail.Right);

    private static TokenListParser<TypeFilterToken, string> KeywordValue =>
        Token.EqualTo(TypeFilterToken.Keyword)
            .Select(static token => token.ToStringValue());

    private static TokenListParser<TypeFilterToken, string> IdentifierValue =>
        Token.EqualTo(TypeFilterToken.Identifier)
            .Select(static token => token.ToStringValue());

    private static TokenListParser<TypeFilterToken, int> NumberValue =>
        Token.EqualTo(TypeFilterToken.Number)
            .Named("number")
            .Select(static token => int.Parse(token.ToStringValue(), CultureInfo.InvariantCulture));

    private static TokenListParser<TypeFilterToken, ValueOperand> ComparisonValue =>
        NumberValue.Select(static number => new ValueOperand(ValueKind.Number, string.Empty, number))
            .Or(KeywordValue.Select(static keyword => new ValueOperand(ValueKind.Keyword, keyword, 0)))
            .Or(IdentifierValue.Select(static identifier => new ValueOperand(ValueKind.Identifier, identifier, 0)));

    private static TokenListParser<TypeFilterToken, (ComparisonOperator Operator, ValueOperand Right)> ComparisonTail =>
        from comparisonOperator in ComparisonOperatorToken
        from right in ComparisonValue.Named("comparison value")
        select (comparisonOperator, right);

    private static TokenListParser<TypeFilterToken, ComparisonOperator> ComparisonOperatorToken =>
        Token.EqualTo(TypeFilterToken.Equal).Value(ComparisonOperator.Equal)
            .Or(Token.EqualTo(TypeFilterToken.NotEqual).Value(ComparisonOperator.NotEqual))
            .Or(Token.EqualTo(TypeFilterToken.GreaterThan).Value(ComparisonOperator.GreaterThan))
            .Or(Token.EqualTo(TypeFilterToken.LessThan).Value(ComparisonOperator.LessThan))
            .Or(Token.EqualTo(TypeFilterToken.GreaterThanOrEqual).Value(ComparisonOperator.GreaterThanOrEqual))
            .Or(Token.EqualTo(TypeFilterToken.LessThanOrEqual).Value(ComparisonOperator.LessThanOrEqual));

    private static Func<PackageModel, bool> BuildComparisonPredicate(
        ValueOperand left,
        ComparisonOperator comparisonOperator,
        ValueOperand right) =>
        package => EvaluateComparison(
            GetOperandValue(package, left),
            comparisonOperator,
            GetOperandValue(package, right));

    private static int GetOperandValue(PackageModel package, ValueOperand operand) =>
        operand.Kind switch
        {
            ValueKind.Number => operand.Number,
            ValueKind.Identifier => GetTypeCount(package, operand.RawValue),
            ValueKind.Keyword => GetKeywordValue(package, operand.RawValue),
            _ => throw new ArgumentOutOfRangeException(nameof(operand), operand.Kind, null)
        };

    private static int GetKeywordValue(PackageModel package, string keyword) =>
        keyword switch
        {
            "%t" or "%types" => package.ExportTypeCount,
            "%e" or "%exports" => package.ExportCount,
            _ => throw new InvalidOperationException($"Unsupported keyword '{keyword}'.")
        };

    private static int GetTypeCount(PackageModel package, string typeReference)
    {
        if (package.TypeCounts.TryGetValue(typeReference, out var exactCount))
            return exactCount;

        foreach (var pair in package.TypeCounts)
        {
            if (string.Equals(pair.Key, typeReference, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return 0;
    }

    private static bool EvaluateComparison(int left, ComparisonOperator comparisonOperator, int right) =>
        comparisonOperator switch
        {
            ComparisonOperator.Equal => left == right,
            ComparisonOperator.NotEqual => left != right,
            ComparisonOperator.GreaterThan => left > right,
            ComparisonOperator.LessThan => left < right,
            ComparisonOperator.GreaterThanOrEqual => left >= right,
            ComparisonOperator.LessThanOrEqual => left <= right,
            _ => throw new ArgumentOutOfRangeException(nameof(comparisonOperator), comparisonOperator, null)
        };
}

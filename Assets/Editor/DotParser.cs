using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Sprache;
using UnityEditor;

public static class Ex
{
    public static string AsNull(this object o) => o == null ? "NULL" : o.ToString();
}

public class DotParser : MonoBehaviour
{
    public class Graph : Statement
    {
        public string Id;
        public List<Statement> Statements;

        public override string ToString() => "graph " + Id + " { " + string.Join(", ", Statements) + " }";
    }

    public class Statement { }

    public class IdAssignment : Statement
    {
        public string From;
        public string To;
    }

    public class Attribute : Statement
    {
        public string Id;
        public string Value;

        public override string ToString() => Id + " = " + Value.AsNull();
    }

    public class NodeStatement : Statement
    {
        public string Id;
        public List<Attribute> Attributes;

        public override string ToString() => Id + " [ " + string.Join(", ", Attributes) + " ]";
    }

    public class Edge : Statement
    {
        public string From;
        public EdgeRHS To;
        public List<Attribute> Attributes;

        public override string ToString() => From + To + " [ " + string.Join(", ", Attributes) + " ]";
    }

    public enum EdgeOp
    {
        DirectedArrow,
        NonDirectedArrow
    }

    public class EdgeRHS
    {
        public EdgeOp EdgeOperation;
        public string NodeId;
        public EdgeRHS Rest;

        public override string ToString() => (EdgeOperation == EdgeOp.DirectedArrow ? " -> " : " -- ") + NodeId + Rest;
    }

    private static readonly Parser<string> SimpleIdentifier =
        from firstLetter in Parse.Letter.Or(Parse.Char('_'))
        from rest in Parse.LetterOrDigit.Or(Parse.Char('_')).Many().Text()
        select firstLetter + rest;

    private static readonly Parser<float> IntegerLiteralLeadingDot =
        from dot in Parse.Char('.')
        from decimals in Parse.Digit.AtLeastOnce().Text()
        select float.Parse(dot + decimals, CultureInfo.InvariantCulture);

    private static readonly Parser<float> IntegerLiteralWithoutLeadingDot =
        from integerPart in Parse.Digit.AtLeastOnce().Text()
        from fractionalPart in (
            from dot in Parse.Char('.')
            from digits in Parse.Digit.Many().Text()
            select dot + digits).Optional()
        select float.Parse(integerPart + fractionalPart.GetOrElse(string.Empty), CultureInfo.InvariantCulture);

    private static readonly Parser<float> IntegerLiteral =
        from minus in Parse.Char('-').Optional()
        from rest in IntegerLiteralLeadingDot.XOr(IntegerLiteralWithoutLeadingDot)
        select (minus.IsDefined ? -1 : 1) * rest;

    private static readonly Parser<string> StringLiteral =
        from _ in Parse.Char('"')
        from str in Parse.AnyChar.Except(Parse.Char('"')).Many().Text()
        from __ in Parse.Char('"')
        select str;

    private static readonly Parser<string> Identifier =
        SimpleIdentifier.XOr(IntegerLiteral.Select(x => x.ToString(CultureInfo.InvariantCulture))).XOr(StringLiteral);

    private static Parser<IdAssignment> AssignmentStatement =
        from leftId in Identifier
        from _ in Parse.Char('=').Token()
        from rightId in Identifier
        select new IdAssignment { From = rightId, To = leftId };

    private static readonly Parser<Attribute> Attr =
        from id in Identifier
        from value in
            (from _ in Parse.String("=").Token()
                from value in Identifier
                select value).Optional()
        select new Attribute { Id = id, Value = value.GetOrDefault() };

    private static readonly Parser<List<Attribute>> AList =
        from attr in Attr.Token()
        from _ in Parse.Char(',').XOr(Parse.Char(';')).Optional()
        from rest in AList.Token().Optional()
        select Enumerable.Repeat(attr, 1).Concat(rest.GetOrElse(Enumerable.Empty<Attribute>())).ToList();

    private static readonly Parser<List<Attribute>> AttributeList =
        from _ in Parse.Char('[')
        from attributes in AList.Optional()
        from __ in Parse.Char(']')
        from rest in (from ___ in Parse.WhiteSpace.Many()
            from list in AttributeList.Optional()
            select list)
        select attributes.GetOrElse(Enumerable.Empty<Attribute>())
            .Concat(rest.GetOrElse(Enumerable.Empty<Attribute>())).ToList();

    private static readonly Parser<EdgeOp> EdgeOpParser =
        from edgeOp in Parse.String("->").Token().Return(EdgeOp.DirectedArrow)
            .Or(Parse.String("--").Token().Return(EdgeOp.NonDirectedArrow))
        select edgeOp;

    private static readonly Parser<EdgeRHS> EdgeRHSParser =
        from edgeOp in EdgeOpParser
        from nodeId in Identifier
        from rest in EdgeRHSParser.Optional()
        select new EdgeRHS { EdgeOperation = edgeOp, NodeId = nodeId, Rest = rest.GetOrDefault() };

    private static readonly Parser<Edge> EdgeParser =
        from nodeId in Identifier
        from rhs in EdgeRHSParser
        from _ in Parse.WhiteSpace.Many()
        from attributes in AttributeList.Optional()
        select new Edge { From = nodeId, To = rhs, Attributes = attributes.GetOrElse(new List<Attribute>()) };

    private static readonly Parser<NodeStatement> Node =
        from id in Identifier
        from _ in Parse.WhiteSpace.Many()
        from attributes in AttributeList.Optional()
        select new NodeStatement { Id = id, Attributes = attributes.GetOrElse(new List<Attribute>()) };

    private static readonly Parser<Graph> Subgraph =
        from graphId in (from _ in Parse.String("subgraph")
            from ____ in Parse.WhiteSpace.Many()
            from id in Identifier.Optional()
            select id.GetOrDefault()).Optional()
        from _____ in Parse.WhiteSpace.Many()
        from __ in Parse.Char('{')
        from statements in StatementList.Token()
        from ___ in Parse.Char('}')
        select new Graph { Id = graphId.GetOrDefault(), Statements = statements };

    private static readonly Parser<Graph> GraphParser =
        from _ in Parse.String("digraph")
        from id in Identifier.Token().Optional()
        from __ in Parse.Char('{')
        from statements in StatementList.Token()
        from ___ in Parse.Char('}')
        select new Graph { Id = id.GetOrDefault(), Statements = statements };

    private static readonly Parser<Statement> StatementParser =
        EdgeParser.Or<Statement>(Subgraph).Or(Node);

    private static readonly Parser<List<Statement>> StatementList =
        from statement in StatementParser
        from _ in Parse.String(";").Optional().Token()
        from rest in StatementList.Optional()
        select Enumerable.Repeat(statement, 1).Concat(rest.GetOrElse(Enumerable.Empty<Statement>())).ToList();

    [MenuItem("Test/Ulala")]
    public static void Test()
    {
        Debug.LogWarning(GraphParser.Parse(File.ReadAllText(Application.dataPath + "/test_input.dot")));
    }
}
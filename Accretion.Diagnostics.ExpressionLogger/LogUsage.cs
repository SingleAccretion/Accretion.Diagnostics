using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.IO;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal readonly struct LogUsage
    {
        public static readonly LogUsage DummyUsage =
            new LogUsage("", null, Location.Create("", new TextSpan(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0))), false);

        public LogUsage(string expressionDefinition, ITypeSymbol type, Location location, bool isPrefixedInvocation)
        {
            var span = location.GetLineSpan();
            FilePath = span.Path;
            LineNumber = (isPrefixedInvocation ? span.StartLinePosition.Line : span.EndLinePosition.Line) + 1;
            Type = type;

            Expression = expressionDefinition;
            Location = location;
        }

        public string FilePath { get; }
        public int LineNumber { get; }
        public ITypeSymbol Type { get; }

        public string Expression { get; }
        public Location Location { get; }

        public bool IsIndistinguishableFrom(LogUsage other) =>
            FilePath == other.FilePath && LineNumber == other.LineNumber &&
           (Type.Kind == SymbolKind.TypeParameter || other.Type.Kind == SymbolKind.TypeParameter || SymbolEqualityComparer.Default.Equals(Type, other.Type));

        public bool IsOnTheSameLineAs(LogUsage other) => FilePath == other.FilePath && LineNumber == other.LineNumber;

        public override string ToString() => $"{Path.GetFileName(FilePath)}:{LineNumber} - {Expression}";
    }
}
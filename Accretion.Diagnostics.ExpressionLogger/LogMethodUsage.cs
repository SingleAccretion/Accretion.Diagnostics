using Microsoft.CodeAnalysis;
using System.IO;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal readonly struct LogMethodUsage
    {
        public LogMethodUsage(string expressionDefinition, ITypeSymbol type, Location location)
        {
            var span = location.GetLineSpan();
            FilePath = span.Path;
            LineNumber = span.StartLinePosition.Line + 1;
            Type = type;

            Expression = expressionDefinition;
            Location = location;
        }

        public string FilePath { get; }
        public int LineNumber { get; }
        public ITypeSymbol Type { get; }

        public string Expression { get; }
        public Location Location { get; }

        public bool IsIndistinguishableFrom(LogMethodUsage other) =>
            FilePath == other.FilePath && LineNumber == other.LineNumber &&
           (Type.Kind == SymbolKind.TypeParameter || other.Type.Kind == SymbolKind.TypeParameter || SymbolEqualityComparer.Default.Equals(Type, other.Type));

        public bool IsOnTheSameLineAs(LogMethodUsage other) => FilePath == other.FilePath && LineNumber == other.LineNumber;

        public override string ToString() => $"{Path.GetFileName(FilePath)}:{LineNumber} - {Expression}";
    }
}
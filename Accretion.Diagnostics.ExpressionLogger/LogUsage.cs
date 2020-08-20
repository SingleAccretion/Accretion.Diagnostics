using Microsoft.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace Accretion.Diagnostics.ExpressionLogger
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct LogUsage
    {
        private static readonly Location _dummyLocation = Location.Create("", default, default);
        public static readonly LogUsage DummyUsage = new LogUsage("", 0, _dummyLocation, _dummyLocation, null!);

        public LogUsage(string expressionDefinition, int lineNumber, Location logLocation, Location expressionLocation, ITypeSymbol type)
        {
            var lineSpan = logLocation.GetLineSpan();
            FilePath = lineSpan.Path;
            LineNumber = lineNumber;
            Type = type;
            Expression = expressionDefinition;
            LogLocation = logLocation;
            ExpressionLocation = expressionLocation;
        }

        public string FilePath { get; }
        public int LineNumber { get; }
        public ITypeSymbol Type { get; }
        public string Expression { get; }

        public Location LogLocation { get; }
        public Location ExpressionLocation { get; }

        public override string ToString() => $"{Path.GetFileName(FilePath)}:{LineNumber} - {Expression}";
    }
}
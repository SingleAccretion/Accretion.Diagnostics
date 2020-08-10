using Microsoft.CodeAnalysis;
using System;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal readonly struct LogMethodUsage : IEquatable<LogMethodUsage>
    {
        public LogMethodUsage(string expressionDefinition, Location location)
        {
            var span = location.GetLineSpan();
            FilePath = span.Path;
            LineNumber = span.StartLinePosition.Line + 1;

            Expression = expressionDefinition;
            Location = location;
        }

        public string FilePath { get; }
        public int LineNumber { get; }

        public string Expression { get; }
        public Location Location { get; }

        public override bool Equals(object obj) => obj is LogMethodUsage usage && Equals(usage);

        public bool Equals(LogMethodUsage other) => FilePath == other.FilePath && LineNumber == other.LineNumber;

        public override int GetHashCode()
        {
            int hashCode = 1137880120;
            hashCode = hashCode * -1521134295 + FilePath.GetHashCode();
            hashCode = hashCode * -1521134295 + LineNumber.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(LogMethodUsage left, LogMethodUsage right) => left.Equals(right);
        public static bool operator !=(LogMethodUsage left, LogMethodUsage right) => !(left == right);
    }
}
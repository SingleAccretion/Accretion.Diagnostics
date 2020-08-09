using System;
using System.Collections.Generic;

namespace Accretion.Diagnostics.FancyDebug
{
    internal readonly struct FancyDebugUsage : IEquatable<FancyDebugUsage>
    {
        public FancyDebugUsage(string expressionDefinition, string filePath, int lineNumber)
        {
            ExpressionDefinition = expressionDefinition;
            FilePath = filePath;
            LineNumber = lineNumber;
        }

        public string ExpressionDefinition { get; }
        public string FilePath { get; }
        public int LineNumber { get; }

        public override bool Equals(object obj) => obj is FancyDebugUsage usage && Equals(usage);

        public bool Equals(FancyDebugUsage other) => ExpressionDefinition == other.ExpressionDefinition && FilePath == other.FilePath && LineNumber == other.LineNumber;

        public override int GetHashCode()
        {
            int hashCode = 1137880120;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ExpressionDefinition);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FilePath);
            hashCode = hashCode * -1521134295 + LineNumber.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(FancyDebugUsage left, FancyDebugUsage right) => left.Equals(right);
        public static bool operator !=(FancyDebugUsage left, FancyDebugUsage right) => !(left == right);
    }
}
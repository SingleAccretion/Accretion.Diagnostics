using Microsoft.CodeAnalysis;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal static class Diagnostics
    {
        private static readonly DiagnosticDescriptor _duplicateLogUsageDescriptor = new DiagnosticDescriptor(
            "ACR000",
            "The expression will not be logged correctly - consider moving it to a separate line",
            "The expression will not be logged correctly - consider moving it to a separate line",
            category: "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static Diagnostic DuplicateLogUsage(Location location) => Diagnostic.Create(_duplicateLogUsageDescriptor, location);
    }
}

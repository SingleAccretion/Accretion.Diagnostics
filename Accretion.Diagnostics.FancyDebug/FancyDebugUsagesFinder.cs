using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Accretion.Diagnostics.FancyDebug
{
    internal class FancyDebugUsagesFinder : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;

        public FancyDebugUsagesFinder(SemanticModel semanticModel) => _semanticModel = semanticModel;

        public List<FancyDebugUsage> Usages { get; } = new List<FancyDebugUsage>();

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            var symbol = symbolInfo.Symbol;
            var symbolName = symbol?.ToDisplayString();

            if (symbolName?.StartsWith("Accretion.Diagnostics.FancyDebug.Fancy.Debug") is true)
            {
                var invocationSite = node.SyntaxTree.GetLineSpan(node.Span);
                var expression = node.ArgumentList.Arguments.First().ToString();
                Usages.Add(new FancyDebugUsage(expression, invocationSite.Path, invocationSite.StartLinePosition.Line + 1));
            }

            base.VisitInvocationExpression(node);
        }
    }
}
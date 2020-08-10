using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal class ExpressionLoggerUsagesFinder : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;

        public ExpressionLoggerUsagesFinder(SemanticModel semanticModel) => _semanticModel = semanticModel;

        public List<FancyDebugUsage> Usages { get; } = new List<FancyDebugUsage>();

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            var method = symbolInfo.Symbol as IMethodSymbol;

            if (method.ContainingNamespace.ToString() == ExpressionLoggerGenerator.ExpressionLoggerClassNamespace &&
                method.ContainingType.Name == ExpressionLoggerGenerator.ExpressionLoggerClassName &&
                method.Name == ExpressionLoggerGenerator.LogMethodName)
            {
                var expression = ExtractExpressionFromInvocation(node);
                var invocationSite = node.SyntaxTree.GetLineSpan(node.Span);

                Usages.Add(new FancyDebugUsage(expression, invocationSite.Path, invocationSite.StartLinePosition.Line + 1));
            }
            
            base.VisitInvocationExpression(node);
        }

        private static string ExtractExpressionFromInvocation(InvocationExpressionSyntax invocation)
        {
            var args = invocation.ArgumentList.Arguments;
            if (args.Count > 0)
            {
                return args[0].ToString();
            }
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Expression.ToString();
            }
            if (invocation.Parent is ConditionalAccessExpressionSyntax conditionalAccess && conditionalAccess.WhenNotNull == invocation)
            {
                return conditionalAccess.Expression.ToString();
            }

            return "This invocation form is not supported by the ExpressionLogger. Please log an issue on GiHub, SingleAccretion/Accretion.Diagnostics.";
        }
    }
}
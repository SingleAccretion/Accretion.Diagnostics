using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using static Accretion.Diagnostics.ExpressionLogger.Identifiers;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal class LogMethodGenerator : CSharpSyntaxWalker
    {
        private readonly Action<Diagnostic> _reportDiagnostic;
        private readonly Compilation _compilation;
        private readonly CodeBuilder _builder;

        private LogUsagesCluster _usagesCluster;
        private SemanticModel _semanticModel;

        public LogMethodGenerator(Action<Diagnostic> reportDiagnostic, Compilation compilation, CodeBuilder builder)
        {
            _reportDiagnostic = reportDiagnostic;
            _compilation = compilation;
            _builder = builder;
        }

        public void GenerateLogMethodBody()
        {
            AuxillariesGenerator.GenerateLogToConsoleMethod(_builder);

            _builder.OpenScope($"switch (({LineNumberParameterName}, {FilePathParameterName}))");
            foreach (var tree in _compilation.SyntaxTrees)
            {
                _semanticModel = _compilation.GetSemanticModel(tree);
                Visit(tree.GetRoot());
            }
            GenerateEnqueuedLogCases();
            _builder.CloseScope();
            
            _builder.AppendLine($"return {ExpressionParameterName};");
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            ExtractLogUsageFromInvocation(node);
            base.VisitInvocationExpression(node);
        }

        private static string ExtractLoggedExpressionFromInvocation(InvocationExpressionSyntax invocation, out bool isStaticInvocation)
        {
            var args = invocation.ArgumentList.Arguments;
            isStaticInvocation = false;

            if (args.Count > 0)
            {
                isStaticInvocation = true;
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

            return "This invocation form is not supported by the ExpressionLogger. Please log an issue on github.com/SingleAccretion/Accretion.Diagnostics.";
        }
        
        private void ExtractLogUsageFromInvocation(InvocationExpressionSyntax invocation)
        {
            if (!invocation.ToString().Contains(LogMethodName))
            {
                return;
            }

            var symbolInfo = _semanticModel.GetSymbolInfo(invocation);

            if (symbolInfo.Symbol is IMethodSymbol method &&
                method.ContainingNamespace.ToString() == ExpressionLoggerClassNamespace &&
                method.ContainingType.Name == ExpressionLoggerClassName &&
                method.Name == LogMethodName)
            {
                var expression = ExtractLoggedExpressionFromInvocation(invocation, out var isStaticInvocation);
                var location = invocation.GetLocation();

                QueueLogCaseGeneration(new LogUsage(expression, method.TypeArguments[0], location, isStaticInvocation));
            }
        }

        private void QueueLogCaseGeneration(LogUsage usage)
        {
            var usages = _usagesCluster;

            if (usages is null)
            {
                _usagesCluster = new LogUsagesCluster(usage);
            }
            else if (usages.AreOnTheSameLineAs(usage))
            {
                if (usages.AreIndistinguishableFrom(usage))
                {
                    _reportDiagnostic(Diagnostics.DuplicateLogUsage(usage.Location));
                    return;
                }

                usages.Add(usage);
            }
            else
            {
                GenerateEnqueuedLogCases();
                usages.Rebase(usage);
            }
        }

        private void GenerateEnqueuedLogCases()
        {
            var usages = _usagesCluster?.Usages;

            if (usages is null || !usages.Any())
            {
                return;
            }

            _builder.AppendLine($"case ({_usagesCluster.LineNumber}, {_usagesCluster.FilePath.AsLiteral()}):");
            if (usages.Count == 1)
            {
                _builder.AppendLine($"LogToConsole({usages[0].Expression.AsLiteral()});");
            }
            else
            {
                for (int i = 0; i < usages.Count; i++)
                {
                    var usage = usages[i];
                    _builder.OpenScope($"if (typeof({usage.Type}) == typeof(T))");
                    _builder.AppendLine($"LogToConsole({usage.Expression.AsLiteral()});");
                    _builder.AppendLine("break;");
                    _builder.CloseScope();
                }
            }

            _builder.AppendLine("break;");
        }
    }
}
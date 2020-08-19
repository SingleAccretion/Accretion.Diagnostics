using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using static Accretion.Diagnostics.ExpressionLogger.Identifiers;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal class LogMethodGenerator : CSharpSyntaxWalker
    {
        private readonly Action<Diagnostic> _reportDiagnostic;
        private readonly Compilation _compilation;
        private readonly CodeBuilder _builder;

        private LogUsagesCluster _lastUsagesCluster;
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
            
            _lastUsagesCluster = new LogUsagesCluster(LogUsage.DummyUsage);
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
            var usages = _lastUsagesCluster;

            if (usages is null)
            {
                _lastUsagesCluster = new LogUsagesCluster(usage);
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
            void GenerateLogToConsoleCall(LogUsage usage) =>
                _builder.AppendLine($"LogToConsole({usage.Expression.AsLiteral()}, {usage.Location.GetLineSpan().StartLinePosition.Character});");

            var usages = _lastUsagesCluster.Usages;

            _builder.AppendLine($"case ({_lastUsagesCluster.LineNumber}, {_lastUsagesCluster.FilePath.AsLiteral()}):");
            if (usages.Count == 1)
            {
                GenerateLogToConsoleCall(usages[0]);
            }
            else
            {
                for (int i = 0; i < usages.Count; i++)
                {
                    var usage = usages[i];
                    _builder.OpenScope($"if (typeof({usage.Type}) == typeof(T))");
                    GenerateLogToConsoleCall(usage);
                    _builder.AppendLine("break;");
                    _builder.CloseScope();
                }
            }

            _builder.AppendLine("break;");
        }
    }
}
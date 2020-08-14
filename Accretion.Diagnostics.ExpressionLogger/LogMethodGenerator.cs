using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static Accretion.Diagnostics.ExpressionLogger.Identifiers;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal class LogMethodGenerator : CSharpSyntaxWalker
    {
        private readonly Action<Diagnostic> _reportDiagnostic;
        private readonly Compilation _compilation;
        private readonly CodeBuilder _builder;

        private SameLineLogMethodUsages _sameLineLogUsages;
        private SemanticModel _semanticModel;

        public LogMethodGenerator(Action<Diagnostic> reportDiagnostic, Compilation compilation, CodeBuilder builder)
        {
            _reportDiagnostic = reportDiagnostic;
            _compilation = compilation;
            _builder = builder;
        }

        public void GenerateLogMethodBody()
        {
            GenerateLogToConsoleMethod();

            _builder.OpenScope($"switch (({LineNumberParameterName}, {FilePathParameterName}))");
            foreach (var tree in _compilation.SyntaxTrees)
            {
                _semanticModel = _compilation.GetSemanticModel(tree);
                Visit(tree.GetRoot());
            }
            //"Flush" the queue and write out the cases on the last line
            GenerateEnqueuedLogCases();
            _builder.CloseScope();

            _builder.AppendLine($"return {ExpressionParameterName};");
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol method &&
                method.ContainingNamespace.ToString() == ExpressionLoggerClassNamespace &&
                method.ContainingType.Name == ExpressionLoggerClassName &&
                method.Name == LogMethodName)
            {
                var expression = ExtractLoggedExpressionFromInvocation(node);
                var location = node.GetLocation();

                QueueLogCaseGeneration(new LogMethodUsage(expression, method.TypeArguments[0], location));
            }

            base.VisitInvocationExpression(node);
        }

        private static string ExtractLoggedExpressionFromInvocation(InvocationExpressionSyntax invocation)
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

            return "This invocation form is not supported by the ExpressionLogger. Please log an issue on github.com/SingleAccretion/Accretion.Diagnostics.";
        }

        private void GenerateLogToConsoleMethod()
        {
            _builder.OpenScope("void LogToConsole(string expressionDefinition)");

            _builder.AppendLine("var color = Console.ForegroundColor;");
            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.DarkGray;");
            _builder.AppendLine("Console.Write(\"[\");");
            _builder.AppendLine($"Console.Write(Path.GetFileName({FilePathParameterName}));");
            _builder.AppendLine("Console.Write(\":\");");
            _builder.AppendLine($"Console.Write({LineNumberParameterName});");
            _builder.AppendLine("Console.Write(\" (\");");
            _builder.AppendLine($"Console.Write({MemberNameParameterName});");
            _builder.AppendLine("Console.Write(\")] \");");

            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.Cyan;");
            _builder.AppendLine($"Console.Write(expressionDefinition);");

            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
            _builder.AppendLine("Console.Write(\" = \");");

            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.Green;");

            _builder.OpenScope($"if (Equals({ExpressionParameterName}, null))");
            _builder.AppendLine("Console.Write(\"null\");");
            _builder.CloseScope();

            _builder.OpenScope("else");
            _builder.AppendLine($"Console.Write({ExpressionParameterName});");
            _builder.AppendLine("Console.Write(\" \");");
            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
            _builder.AppendLine("Console.Write(\"(\");");
            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.DarkCyan;");
            _builder.AppendLine($"Console.Write({ExpressionParameterName}.GetType().Name);");
            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
            _builder.AppendLine("Console.Write(\")\");");
            _builder.CloseScope();

            _builder.AppendLine("Console.ForegroundColor = color;");
            _builder.AppendLine("Console.WriteLine();");

            _builder.CloseScope();
        }

        private void QueueLogCaseGeneration(LogMethodUsage usage)
        {
            var usages = _sameLineLogUsages;

            if (usages is null)
            {
                _sameLineLogUsages = new SameLineLogMethodUsages(usage);
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
            var usages = _sameLineLogUsages?.Usages;
            Debug.Assert(usages != null && usages.Any());

            _builder.AppendLine($"case ({_sameLineLogUsages.LineNumber}, {_sameLineLogUsages.FilePath.AsLiteral()}):");
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
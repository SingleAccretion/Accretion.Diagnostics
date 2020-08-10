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

        private LogMethodUsage _lastDetectedLogMethodUsage;
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

                GenerateLogCase(new LogMethodUsage(expression, location));
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
            _builder.AppendLine($"Console.Write(Path.GetFileName(filePath));");
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
            _builder.AppendLine($"Console.Write({ExpressionParameterName}.GetType());");
            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
            _builder.AppendLine("Console.Write(\")\");");
            _builder.CloseScope();

            _builder.AppendLine("Console.ForegroundColor = color;");
            _builder.AppendLine("Console.WriteLine();");

            _builder.CloseScope();
        }

        private void GenerateLogCase(LogMethodUsage usage)
        {
            if (_lastDetectedLogMethodUsage == usage)
            {
                _reportDiagnostic(Diagnostics.DuplicateLogUsage(usage.Location));
                return;
            }

            var pathLiteral = usage.FilePath.AsLiteral();
            var expressionDefinitionLiteral = usage.Expression.AsLiteral();

            _builder.AppendLine($"case ({usage.LineNumber}, {pathLiteral}):");
            _builder.AppendLine($"LogToConsole({expressionDefinitionLiteral});");
            _builder.AppendLine("break;");

            _lastDetectedLogMethodUsage = usage;
        }
    }
}
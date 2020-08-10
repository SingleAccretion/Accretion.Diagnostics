using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Accretion.Diagnostics.ExpressionLogger
{
    [Generator]
    public class ExpressionLoggerGenerator : ISourceGenerator
    {
        public const string FullyQualifiedLogMethodName = ExpressionLoggerClassNamespace + "." + ExpressionLoggerClassName + "." + LogMethodName;
        public const string ExpressionLoggerClassNamespace = "Accretion.Diagnostics.ExpressionLogger";
        public const string ExpressionLoggerClassName = "ExpressionLogger";
        public const string LogMethodName = "Log";

        public void Execute(SourceGeneratorContext context)
        {
            var compilation = AddExpressionLoggerClassToCompilation(context.Compilation);

            IEnumerable<FancyDebugUsage> FancyDebugUsages()
            {
                foreach (var tree in compilation.SyntaxTrees)
                {
                    var finder = new ExpressionLoggerUsagesFinder(compilation.GetSemanticModel(tree));
                    finder.Visit(tree.GetRoot());

                    foreach (var usage in finder.Usages)
                    {
                        yield return usage;
                    }
                }
            }

            var logMethodSource = GenerateLogMethodSource(FancyDebugUsages());
            context.AddSource("ExpressionLogger.cs", logMethodSource);
        }

        public void Initialize(InitializationContext context)
        {
            //Debugger.Launch();
        }

        private static void EmitDignostic(string message, SourceGeneratorContext context) =>
            context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(Guid.NewGuid().ToString().Substring(0, 4), "", message, "correctness", DiagnosticSeverity.Warning, true), null, new object[] { }));

        private static Compilation AddExpressionLoggerClassToCompilation(Compilation originalCompilation)
        {
            var languageVersion = ((CSharpParseOptions)originalCompilation.SyntaxTrees.FirstOrDefault().Options).LanguageVersion;
            var parsingOptions = new CSharpParseOptions(languageVersion);

            var expressionLoggerClassDefinitionSource = GenerateExpressionLoggerClassSource();
            var expressionLoggerClassSyntaxTree = SyntaxFactory.ParseSyntaxTree(expressionLoggerClassDefinitionSource, parsingOptions, isGeneratedCode: true);

            return originalCompilation.AddSyntaxTrees(expressionLoggerClassSyntaxTree);
        }

        private static SourceText GenerateExpressionLoggerClassSource(Action<CodeBuilder> logMethodGenerator = null)
        {
            var builder = new CodeBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.IO;");
            builder.AppendLine("using System.Runtime.CompilerServices;");
            builder.OpenScope($"namespace {ExpressionLoggerClassNamespace}");
            builder.OpenScope($"public static class {ExpressionLoggerClassName}");

            builder.OpenScope($"public static T {LogMethodName}<T>(this T expression, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = null, [CallerMemberName] string memberName = null)");
            logMethodGenerator?.Invoke(builder);
            builder.CloseScope();

            builder.CloseScope();
            builder.CloseScope();

            return SourceText.From(builder.ToString(), Encoding.UTF8);
        }

        private static SourceText GenerateLogMethodSource(IEnumerable<FancyDebugUsage> fancyDebugUsages) => GenerateExpressionLoggerClassSource(builder =>
        {
            if (fancyDebugUsages.Any())
            {
                builder.OpenScope("void LogToConsole(string expressionDefinition)");
                
                builder.AppendLine("var color = Console.ForegroundColor;");
                builder.AppendLine("Console.ForegroundColor = ConsoleColor.DarkGray;");
                builder.AppendLine("Console.Write(\"[\");");
                builder.AppendLine($"Console.Write(Path.GetFileName(filePath));");
                builder.AppendLine("Console.Write(\":\");");
                builder.AppendLine("Console.Write(lineNumber);");
                builder.AppendLine("Console.Write(\" \");");
                builder.AppendLine("Console.Write(\"(\");");
                builder.AppendLine("Console.Write(memberName);");
                builder.AppendLine("Console.Write(\")\");");
                builder.AppendLine("Console.Write(\"]\");");

                builder.AppendLine("Console.Write(\" \");");

                builder.AppendLine("Console.ForegroundColor = ConsoleColor.Cyan;");
                builder.AppendLine($"Console.Write(expressionDefinition);");

                builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
                builder.AppendLine("Console.Write(\" = \");");

                builder.AppendLine("Console.ForegroundColor = ConsoleColor.Green;");

                builder.OpenScope("if (Equals(expression, null))");
                builder.AppendLine("Console.Write(\"null\");");
                builder.CloseScope();

                builder.OpenScope("else");
                builder.AppendLine("Console.Write(expression);");
                builder.AppendLine("Console.Write(\" \");");
                builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
                builder.AppendLine("Console.Write(\"(\");");
                builder.AppendLine("Console.ForegroundColor = ConsoleColor.DarkCyan;");
                builder.AppendLine("Console.Write(expression.GetType());");
                builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
                builder.AppendLine("Console.Write(\")\");");
                builder.CloseScope();

                builder.AppendLine("Console.ForegroundColor = color;");
                builder.AppendLine("Console.WriteLine();");

                builder.CloseScope();

                builder.OpenScope("switch ((lineNumber, filePath))");

                foreach (var usage in fancyDebugUsages)
                {
                    var pathLiteral = EscapedString(usage.FilePath);
                    var expressionDefinitionLiteral = EscapedString(usage.Expression);

                    builder.AppendLine($"case ({usage.LineNumber}, {pathLiteral}):");
                    builder.AppendLine($"LogToConsole({expressionDefinitionLiteral});");
                    builder.AppendLine("break;");
                }

                builder.CloseScope();
            }
            
            builder.AppendLine("return expression;");
        });

        private static string EscapedString(string source) => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(source ?? "null")).ToString();
    }
}
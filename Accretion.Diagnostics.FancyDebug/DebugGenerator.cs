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

namespace Accretion.Diagnostics.FancyDebug
{
    [Generator]
    public class DebugGenerator : ISourceGenerator
    {
        public void Execute(SourceGeneratorContext context) 
        {
            var compilation = DefineFancyClass(context);

            IEnumerable<FancyDebugUsage> FancyDebugUsages()
            {
                foreach (var tree in compilation.SyntaxTrees)
                {
                    var finder = new FancyDebugUsagesFinder(compilation.GetSemanticModel(tree));
                    finder.Visit(tree.GetRoot());

                    foreach (var usage in finder.Usages)
                    {
                        EmitDignostic(usage.FilePath + " " + usage.ExpressionDefinition + " " + usage.LineNumber, context);
                        yield return usage;
                    }
                }
            }

            var logMethodSource = CreateLogMethodSource(FancyDebugUsages());
            var logMethodSourceText = SourceText.From(logMethodSource, Encoding.UTF8);
            context.AddSource("Fancy.Log.cs", logMethodSourceText);
        }

        public void Initialize(InitializationContext context)
        { 
            //Debugger.Launch();
        }

        private static void EmitDignostic(string message, SourceGeneratorContext context) => 
            context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(Guid.NewGuid().ToString().Substring(0, 4), "", message, "correctness", DiagnosticSeverity.Warning, true), null, new object[] { }));

        private static Compilation DefineFancyClass(SourceGeneratorContext generatorContext)
        {
            var originalCompilation = generatorContext.Compilation;
            var languageVersion = ((CSharpParseOptions)originalCompilation.SyntaxTrees.FirstOrDefault().Options).LanguageVersion;
            
            var debugClassDefinition = SourceText.From(@"
                using System.Runtime.CompilerServices;
                namespace Accretion.Diagnostics.FancyDebug
                {
                    public static partial class Fancy
                    {
                        public static T Debug<T>(T expression, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = null, [CallerMemberName] string memberName = null)
                        {
                            Log(expression, lineNumber, filePath, memberName);
                            return expression;
                        }

                        static partial void Log(object expression, int lineNumber, string filePath, string memberName);
                    }
                }", Encoding.UTF8);

            var debugClassSyntaxTree = SyntaxFactory.ParseSyntaxTree(debugClassDefinition, new CSharpParseOptions(languageVersion), isGeneratedCode: true);

            generatorContext.AddSource("Fancy.cs", debugClassDefinition);
            return originalCompilation.AddSyntaxTrees(debugClassSyntaxTree);
        }

        private static string CreateLogMethodSource(IEnumerable<FancyDebugUsage> fancyDebugUsages)
        {
            var builder = new CodeBuilder();
            builder.AppendLine("using System;");
            builder.OpenScope("namespace Accretion.Diagnostics.FancyDebug");
            builder.OpenScope("public static partial class Fancy");
            builder.OpenScope("static partial void Log(object expression, int lineNumber, string filePath, string memberName)");

            builder.AppendLine("ConsoleColor color;");
            builder.OpenScope("switch ((lineNumber, filePath))");
            foreach (var usage in fancyDebugUsages)
            {
                var pathLiteral = EscapedString(usage.FilePath);
                var fileNameLiteral = EscapedString(Path.GetFileName(usage.FilePath));
                var expressionDefinitionLiteral = EscapedString(usage.ExpressionDefinition);

                builder.AppendLine($"case ({usage.LineNumber}, {pathLiteral}):");
                
                builder.AppendLine("color = Console.ForegroundColor;");
                builder.AppendLine("Console.ForegroundColor = ConsoleColor.Gray;");
                builder.AppendLine("Console.Write(\"[\");");
                builder.AppendLine($"Console.Write({fileNameLiteral});");
                builder.AppendLine("Console.Write(\":\");");
                builder.AppendLine("Console.Write(lineNumber);");
                builder.AppendLine("Console.Write(\" \");");
                builder.AppendLine("Console.Write(\"(\");");
                builder.AppendLine("Console.Write(memberName);");
                builder.AppendLine("Console.Write(\")\");");
                builder.AppendLine("Console.Write(\"]\");");

                builder.AppendLine("Console.Write(\" \");");
                
                builder.AppendLine("Console.ForegroundColor = ConsoleColor.Cyan;");
                builder.AppendLine($"Console.Write({expressionDefinitionLiteral});");
                
                builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
                builder.AppendLine("Console.Write(\" = \");");
                
                builder.AppendLine("Console.ForegroundColor = ConsoleColor.Blue;");
                builder.AppendLine("Console.Write(expression);");

                builder.AppendLine("Console.Write(\" \");");

                builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
                builder.AppendLine("Console.Write(\"(\");");
                builder.AppendLine("Console.ForegroundColor = ConsoleColor.Green;");
                builder.AppendLine("Console.Write(expression.GetType());");
                builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
                builder.AppendLine("Console.Write(\")\");");


                builder.AppendLine("Console.ForegroundColor = color;");
                builder.AppendLine("Console.WriteLine();");

                builder.AppendLine("return;");
            }
            builder.CloseScope();

            builder.CloseScope();
            builder.CloseScope();
            builder.CloseScope();

            return builder.ToString();
        }

        private static string EscapedString(string source) => 
            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(source)).ToString();
    }
}
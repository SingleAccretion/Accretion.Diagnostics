using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Diagnostics;
using System.Linq;
using static Accretion.Diagnostics.ExpressionLogger.Identifiers;

namespace Accretion.Diagnostics.ExpressionLogger
{
    [Generator]
    public class LoggerClassEmitter : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var collector = (LogUsagesCollector)context.SyntaxReceiver!;
            var builder = new CodeBuilder();
            
            var compilation = AddExpressionLoggerClassToCompilation(context.Compilation, out var logMethodDefinition);
            var usages = collector.CollectUsages(compilation, logMethodDefinition, context.ReportDiagnostic);
            
            EmitExpressionLoggerClassSource(builder, x => LogMethodEmitter.EmitLogMethodBody(usages, x));
            context.AddSource("ExpressionLogger.cs", builder.ToSourceText());
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new LogUsagesCollector());
            //Debugger.Launch();
        }

        private static Compilation AddExpressionLoggerClassToCompilation(Compilation originalCompilation, out IMethodSymbol logMethodDefinition)
        {
            var builder = new CodeBuilder();
            var languageVersion = ((CSharpParseOptions)originalCompilation.SyntaxTrees.FirstOrDefault().Options).LanguageVersion;
            var parsingOptions = new CSharpParseOptions(languageVersion);

            EmitExpressionLoggerClassSource(builder);

            var source = builder.ToSourceText();
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(source, parsingOptions);
            var compilation = originalCompilation.AddSyntaxTrees(syntaxTree);
            logMethodDefinition = (IMethodSymbol)compilation.GetSemanticModel(syntaxTree).
                GetDeclaredSymbol(syntaxTree.GetRoot().DescendantNodes().
                    First(x => x is MethodDeclarationSyntax method && method.Identifier.ToString() == LogMethodName))!;

            return compilation;
        }

        private static void EmitExpressionLoggerClassSource(CodeBuilder builder, Action<CodeBuilder>? logMethodGenerator = null)
        {
            builder.AppendLine("using System;");
            builder.AppendLine("using System.IO;");
            builder.AppendLine("using System.Linq;");
            builder.AppendLine("using System.Collections;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Text.RegularExpressions;");
            builder.AppendLine("using System.Runtime.CompilerServices;");
            builder.OpenScope($"namespace {ExpressionLoggerClassNamespace}");
            builder.OpenScope($"public static class {ExpressionLoggerClassName}");

            builder.OpenScope($"public static T {FullLogMethodName}(this T {ExpressionParameterName}, [CallerLineNumber] int {LineNumberParameterName} = 0, [CallerFilePath] string {FilePathParameterName} = null, [CallerMemberName] string {MemberNameParameterName} = null)");
            logMethodGenerator?.Invoke(builder);
            builder.CloseScope();

            builder.CloseScope();
            builder.CloseScope();
        }
    }
}
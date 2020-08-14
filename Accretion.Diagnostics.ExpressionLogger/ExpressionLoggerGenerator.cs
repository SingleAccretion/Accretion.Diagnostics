using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static Accretion.Diagnostics.ExpressionLogger.Identifiers;

namespace Accretion.Diagnostics.ExpressionLogger
{
    [Generator]
    public class ExpressionLoggerGenerator : ISourceGenerator
    {
        public void Execute(SourceGeneratorContext context)
        {
            var compilation = AddExpressionLoggerClassToCompilation(context.Compilation);
            var logMethodSource = GenerateExpressionLoggerClassSource(bulder => new LogMethodGenerator(d => context.ReportDiagnostic(d), compilation, bulder).GenerateLogMethodBody());
            context.AddSource("ExpressionLogger.cs", logMethodSource);
        }

        public void Initialize(InitializationContext context)
        {
            //Debugger.Launch();
        }

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
            builder.AppendLine("using System.Linq;");
            builder.AppendLine("using System.Collections;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Runtime.CompilerServices;");
            builder.OpenScope($"namespace {ExpressionLoggerClassNamespace}");
            builder.OpenScope($"public static class {ExpressionLoggerClassName}");

            builder.OpenScope($"public static T {LogMethodName}<T>(this T {ExpressionParameterName}, [CallerLineNumber] int {LineNumberParameterName} = 0, [CallerFilePath] string {FilePathParameterName} = null, [CallerMemberName] string {MemberNameParameterName} = null)");
            logMethodGenerator?.Invoke(builder);
            builder.CloseScope();

            builder.CloseScope();
            builder.CloseScope();

            return SourceText.From(builder.ToString(), Encoding.UTF8);
        }
    }
}
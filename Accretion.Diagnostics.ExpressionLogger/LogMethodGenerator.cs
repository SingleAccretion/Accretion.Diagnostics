﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            GeneratePrettyTypeNameMethod();
            GeneratePrettyValueStringMethod();

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
            _builder.AppendLine($"Console.Write({PrettyValueStringMethodName}({ExpressionParameterName}));");
            _builder.AppendLine("Console.Write(\" \");");
            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
            _builder.AppendLine("Console.Write(\"(\");");
            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.DarkCyan;");
            _builder.AppendLine($"Console.Write({PrettyTypeNameMethodName}({ExpressionParameterName}.GetType()));");
            _builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
            _builder.AppendLine("Console.Write(\")\");");
            _builder.CloseScope();

            _builder.AppendLine("Console.ForegroundColor = color;");
            _builder.AppendLine("Console.WriteLine();");

            _builder.CloseScope();
        }

        private void GeneratePrettyTypeNameMethod()
        {
            /*
            static string PrettyTypeName(Type type) => type switch
            {
                { IsArray: true } => $"{PrettyTypeName(type.GetElementType())}[]",
                { IsPointer: true } => $"{PrettyTypeName(type.GetElementType())}*",
                { IsByRef: true } => $"{PrettyTypeName(type.GetElementType())}&",
                _ when Nullable.GetUnderlyingType(type) is Type t => $"{PrettyTypeName(t)}?",
                { IsGenericType: true } => $"{type.Name.Remove(type.Name.IndexOf('`'))}<{string.Join(", ", type.GenericTypeArguments.Select(x => PrettyTypeName(x)))}>",
                _ => type.Name
            };
            */

            _builder.OpenScope($"static string {PrettyTypeNameMethodName}(Type type) => type switch");

            _builder.AppendLine("{ IsArray: true } => $\"{PrettyTypeName(type.GetElementType())}[]\",");
            _builder.AppendLine("{ IsPointer: true } => $\"{PrettyTypeName(type.GetElementType())}*\",");
            _builder.AppendLine("{ IsByRef: true } => $\"{PrettyTypeName(type.GetElementType())}&\",");
            _builder.AppendLine("_ when Nullable.GetUnderlyingType(type) is Type t => $\"{PrettyTypeName(t)}?\",");
            _builder.AppendLine("{ IsGenericType: true } => $\"{type.Name.Remove(type.Name.IndexOf('`'))}<{string.Join(\", \", type.GenericTypeArguments.Select(x => PrettyTypeName(x)))}>\",");
            _builder.AppendLine("_ => type.Name");

            _builder.CloseScope(";");
        }

        private void GeneratePrettyValueStringMethod()
        {
            /*
            static string PrettyValueString(object value) => value switch
            {
                null => "null",
                string s => s,
                IEnumerable list => list.Cast<object>().Any() ? "{ " + string.Join(", ", list.Cast<object>().Select(x => PrettyValueString(x))) + " }" : "{ }",
                //Type type => PrettyTypeName(type),
                _ => value.ToString()
            };
            */

            _builder.OpenScope("static string PrettyValueString(object value) => value switch");
            _builder.AppendLine("null => \"null\",");
            _builder.AppendLine("string s => s,");
            _builder.AppendLine("IEnumerable list => list.Cast<object>().Any() ? \"{ \" + string.Join(\", \", list.Cast<object>().Select(x => PrettyValueString(x))) + \" }\" : \"{ }\",");
            _builder.AppendLine($"Type type => {PrettyTypeNameMethodName}(type),");
            _builder.AppendLine("_ => value.ToString()");
            _builder.CloseScope(";");
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
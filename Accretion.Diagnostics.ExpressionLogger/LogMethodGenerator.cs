using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
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
            GenerateEnqueuedLogCases();
            _builder.CloseScope();

            _builder.AppendLine($"return {ExpressionParameterName};");
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (!node.ToString().Contains(LogMethodName))
            {
                return;
            }

            var symbolInfo = _semanticModel.GetSymbolInfo(node);

            if (symbolInfo.Symbol is IMethodSymbol method &&
                method.ContainingNamespace.ToString() == ExpressionLoggerClassNamespace &&
                method.ContainingType.Name == ExpressionLoggerClassName &&
                method.Name == LogMethodName)
            {
                var expression = ExtractLoggedExpressionFromInvocation(node, out var isStaticInvocation);
                var location = node.GetLocation();

                QueueLogCaseGeneration(new LogMethodUsage(expression, method.TypeArguments[0], location, isStaticInvocation));
            }

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
            static string PrettyTypeName(Type type)
            {
                IEnumerable<string> NestedTypes(Type child)
                {
                    IEnumerable<Type> EnumerateTypesChildToParent()
                    {
                        yield return child;
                        while (child.DeclaringType is Type parent)
                        {
                            yield return parent;
                            child = parent;
                        }
                    }

                    var genericArgs = child.GetGenericArguments();
                    var argsTaken = 0;
                    foreach (var type in EnumerateTypesChildToParent().Reverse())
                    {
                        var argsToTake = type.GetGenericArguments().Length - argsTaken;
                        var args = genericArgs.Skip(argsTaken).Take(argsToTake);

                        yield return Regex.Replace(type.Name, "`\\d+$", $"<{string.Join(", ", args.Select(x => PrettyTypeName(x)))}>");
                        argsTaken += argsToTake;
                    }
                }
                
                return type switch
                {
                    { IsArray: true } => $"{PrettyTypeName(type.GetElementType())}[]",
                    { IsPointer: true } => $"{PrettyTypeName(type.GetElementType())}*",
                    { IsByRef: true } => $"{PrettyTypeName(type.GetElementType())}&",
                    _ when Nullable.GetUnderlyingType(type) is Type t => $"{PrettyTypeName(t)}?",
                    _ => string.Join(".", NestedTypes(type))
                };
            }

            _builder.OpenScope($"static string {PrettyTypeNameMethodName}(Type type, bool includeParent = true)");

            _builder.OpenScope("IEnumerable<string> NestedTypes(Type child)");
            
            _builder.OpenScope("IEnumerable<Type> EnumerateTypesChildToParent()");
            _builder.AppendLine("yield return child;");
            _builder.OpenScope("while (child.DeclaringType is Type parent)");
            _builder.AppendLine("yield return parent;");
            _builder.AppendLine("child = parent;");
            _builder.CloseScope();
            _builder.CloseScope();

            _builder.AppendLine("var genericArgs = child.GetGenericArguments();");
            _builder.AppendLine("var argsTaken = 0;");
            _builder.OpenScope("foreach (var type in EnumerateTypesChildToParent().Reverse())");
            _builder.AppendLine("var argsToTake = type.GetGenericArguments().Length - argsTaken;");
            _builder.AppendLine("var args = genericArgs.Skip(argsTaken).Take(argsToTake);");
            _builder.AppendLine("yield return Regex.Replace(type.Name, \"`\\\\d+$\", $\"<{string.Join(\", \", args.Select(x => PrettyTypeName(x)))}>\");");
            _builder.AppendLine("argsTaken += argsToTake;");
            _builder.CloseScope();

            _builder.CloseScope();

            _builder.OpenScope("return type switch");
            _builder.AppendLine("{ IsArray: true } => $\"{PrettyTypeName(type.GetElementType())}[]\",");
            _builder.AppendLine("{ IsPointer: true } => $\"{PrettyTypeName(type.GetElementType())}*\",");
            _builder.AppendLine("{ IsByRef: true } => $\"{PrettyTypeName(type.GetElementType())}&\",");
            _builder.AppendLine("_ when Nullable.GetUnderlyingType(type) is Type t => $\"{PrettyTypeName(t)}?\",");
            _builder.AppendLine("_ => string.Join(\".\", NestedTypes(type))");
            _builder.CloseScope(";");

            _builder.CloseScope();
        }

        private void GeneratePrettyValueStringMethod()
        {
            /*
            static string PrettyValueString(object value)
            {
                static string PrettyEnumerableString(IEnumerable enumerable)
                {
                    var sequence = enumerable.Cast<object>();
                    return sequence.Any() ? "{ " + string.Join(", ", sequence.Select(x => PrettyValueString(x))) + " }" : "{ }";
                }
                
                return value switch
                {
                    null => "null",
                    char ch => $"'{ch}'",
                    string s => $@"""{s}""",
                    object kv when kv.GetType() is { IsGenericType: true, IsValueType: true } type &&
                                   type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>) =>
                                 $"{PrettyValueString(type.GetProperty("Key").GetValue(kv))}: {PrettyValueString(type.GetProperty("Value").GetValue(kv))}",
                    //Type type => PrettyTypeName(type),
                    IEnumerable enumerable => PrettyEnumerableString(enumerable),
                    //_ when value.ToString() == value.GetType().FullName => PrettyTypeName(value.GetType()),
                    _ => value.ToString()
                };
            }
            */

            _builder.OpenScope("static string PrettyValueString(object value)");

            _builder.OpenScope("static string PrettyEnumerableString(IEnumerable enumerable, bool useDefaultToString = false)");
            _builder.AppendLine("var sequence = enumerable.Cast<object>();");
            _builder.AppendLine("return sequence.Any() ?");
            _builder.AppendLine("\"{ \" + string.Join(\", \", sequence.Select(x => useDefaultToString ? x.ToString() : PrettyValueString(x))) + \" }\" :");
            _builder.AppendLine("\"{ }\";");
            _builder.CloseScope();

            _builder.OpenScope("return value switch");
            _builder.AppendLine("null => \"null\",");
            _builder.AppendLine("char ch => $\"'{ch}'\",");
            _builder.AppendLine("string s => $@\"\"\"{s}\"\"\",");
            _builder.AppendLine("object kv when kv.GetType() is { IsGenericType: true, IsValueType: true } type && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>) => $\"{PrettyValueString(type.GetProperty(\"Key\").GetValue(kv))}: {PrettyValueString(type.GetProperty(\"Value\").GetValue(kv))}\",");
            _builder.AppendLine($"Type type => {PrettyTypeNameMethodName}(type),");
            _builder.AppendLine("IEnumerable enumerable => PrettyEnumerableString(enumerable),");
            _builder.AppendLine($"_ when value.ToString() == value.GetType().ToString() => {PrettyTypeNameMethodName}(value.GetType()),");
            _builder.AppendLine("_ => value.ToString()");
            _builder.CloseScope(";");

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

            if (usages is null || !usages.Any())
            {
                return;
            }

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
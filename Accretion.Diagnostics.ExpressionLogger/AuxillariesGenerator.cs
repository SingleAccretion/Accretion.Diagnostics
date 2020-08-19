using static Accretion.Diagnostics.ExpressionLogger.Identifiers;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal static class AuxillariesGenerator
    {
        public static void GenerateLogToConsoleMethod(CodeBuilder builder)
        {
            GeneratePrettyTypeNameMethod(builder);
            GeneratePrettyValueStringMethod(builder);

            builder.OpenScope("void LogToConsole(string expressionDefinition)");

            builder.AppendLine("var color = Console.ForegroundColor;");
            builder.AppendLine("Console.ForegroundColor = ConsoleColor.DarkGray;");
            builder.AppendLine($"Console.Write($\"[{{Path.GetFileName({FilePathParameterName})}}:{{{LineNumberParameterName}}} ({{{MemberNameParameterName}}})] \");");

            builder.AppendLine("Console.ForegroundColor = ConsoleColor.Cyan;");
            builder.AppendLine($"Console.Write(expressionDefinition);");

            builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
            builder.AppendLine("Console.Write(\" = \");");

            builder.AppendLine("Console.ForegroundColor = ConsoleColor.Green;");

            builder.OpenScope($"if (Equals({ExpressionParameterName}, null))");
            builder.AppendLine("Console.Write(\"null\");");
            builder.CloseScope();

            builder.OpenScope("else");
            builder.AppendLine($"Console.Write({PrettyValueStringMethodName}({ExpressionParameterName}));");
            builder.AppendLine("Console.Write(\" \");");
            builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
            builder.AppendLine("Console.Write(\"(\");");
            builder.AppendLine("Console.ForegroundColor = ConsoleColor.DarkCyan;");
            builder.AppendLine($"Console.Write({PrettyTypeNameMethodName}({ExpressionParameterName}.GetType()));");
            builder.AppendLine("Console.ForegroundColor = ConsoleColor.White;");
            builder.AppendLine("Console.Write(\")\");");
            builder.CloseScope();

            builder.AppendLine("Console.ForegroundColor = color;");
            builder.AppendLine("Console.WriteLine();");

            builder.CloseScope();
        }

        public static void GeneratePrettyTypeNameMethod(CodeBuilder builder)
        {
            /*
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
            */

            builder.OpenScope($"static string {PrettyTypeNameMethodName}(Type type, bool includeParent = true)");

            builder.OpenScope("IEnumerable<string> NestedTypes(Type child)");

            builder.OpenScope("IEnumerable<Type> EnumerateTypesChildToParent()");
            builder.AppendLine("yield return child;");
            builder.OpenScope("while (child.DeclaringType is Type parent)");
            builder.AppendLine("yield return parent;");
            builder.AppendLine("child = parent;");
            builder.CloseScope();
            builder.CloseScope();

            builder.AppendLine("var genericArgs = child.GetGenericArguments();");
            builder.AppendLine("var argsTaken = 0;");
            builder.OpenScope("foreach (var type in EnumerateTypesChildToParent().Reverse())");
            builder.AppendLine("var argsToTake = type.GetGenericArguments().Length - argsTaken;");
            builder.AppendLine("var args = genericArgs.Skip(argsTaken).Take(argsToTake);");
            builder.AppendLine("yield return Regex.Replace(type.Name, \"`\\\\d+$\", $\"<{string.Join(\", \", args.Select(x => PrettyTypeName(x)))}>\");");
            builder.AppendLine("argsTaken += argsToTake;");
            builder.CloseScope();

            builder.CloseScope();

            builder.OpenScope("return type switch");
            builder.AppendLine("{ IsArray: true } => $\"{PrettyTypeName(type.GetElementType())}[]\",");
            builder.AppendLine("{ IsPointer: true } => $\"{PrettyTypeName(type.GetElementType())}*\",");
            builder.AppendLine("{ IsByRef: true } => $\"{PrettyTypeName(type.GetElementType())}&\",");
            builder.AppendLine("_ when Nullable.GetUnderlyingType(type) is Type t => $\"{PrettyTypeName(t)}?\",");
            builder.AppendLine("_ => string.Join(\".\", NestedTypes(type))");
            builder.CloseScope(";");

            builder.CloseScope();
        }

        public static void GeneratePrettyValueStringMethod(CodeBuilder builder)
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

            builder.OpenScope("static string PrettyValueString(object value)");

            builder.OpenScope("static string PrettyEnumerableString(IEnumerable enumerable, bool useDefaultToString = false)");
            builder.AppendLine("var sequence = enumerable.Cast<object>();");
            builder.AppendLine("return sequence.Any() ?");
            builder.AppendLine("\"{ \" + string.Join(\", \", sequence.Select(x => useDefaultToString ? x.ToString() : PrettyValueString(x))) + \" }\" :");
            builder.AppendLine("\"{ }\";");
            builder.CloseScope();

            builder.OpenScope("return value switch");
            builder.AppendLine("null => \"null\",");
            builder.AppendLine("char ch => $\"'{ch}'\",");
            builder.AppendLine("string s => $@\"\"\"{s}\"\"\",");
            builder.AppendLine("object kv when kv.GetType() is { IsGenericType: true, IsValueType: true } type && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>) => $\"{PrettyValueString(type.GetProperty(\"Key\").GetValue(kv))}: {PrettyValueString(type.GetProperty(\"Value\").GetValue(kv))}\",");
            builder.AppendLine($"Type type => {PrettyTypeNameMethodName}(type),");
            builder.AppendLine("IEnumerable enumerable => PrettyEnumerableString(enumerable),");
            builder.AppendLine($"_ when value.ToString() == value.GetType().ToString() => {PrettyTypeNameMethodName}(value.GetType()),");
            builder.AppendLine("_ => value.ToString()");
            builder.CloseScope(";");

            builder.CloseScope();
        }
    }
}

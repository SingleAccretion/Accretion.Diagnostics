using System.Collections.Generic;
using static Accretion.Diagnostics.ExpressionLogger.Identifiers;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal static class LogMethodEmitter
    {
        public static void EmitLogMethodBody(IEnumerable<LogUsage> usages, CodeBuilder builder)
        {
            FormattingMethodsEmitter.EmitLogToConsoleMethod(builder);
            
            builder.OpenScope($"switch (({FilePathParameterName}, {LineNumberParameterName}))");
            (string FilePath, int LineNumber) lastPositon = default;
            var enqueuedUsages = new List<LogUsage>() { LogUsage.DummyUsage };
            foreach (var usage in usages)
            {
                if ((usage.FilePath, usage.LineNumber) == lastPositon)
                {
                    enqueuedUsages.Add(usage);
                }
                else
                {
                    EmitEnqueuedLogUsages(enqueuedUsages, builder);
                    enqueuedUsages.Clear();
                    enqueuedUsages.Add(usage);
                    lastPositon = (usage.FilePath, usage.LineNumber);
                }
            }
            builder.CloseScope();

            builder.AppendLine($"return {ExpressionParameterName};");
        }

        private static void EmitEnqueuedLogUsages(List<LogUsage> usages, CodeBuilder builder)
        {
            void EmitLogToConsoleCall(LogUsage usage) =>
                builder.AppendLine($"LogToConsole({usage.Expression.AsLiteral()}, {usage.ExpressionLocation.GetLineSpan().StartLinePosition.Character});");

            var filePath = usages[0].FilePath;
            var lineNumber = usages[0].LineNumber;
            builder.AppendLine($"case ({filePath.AsLiteral()}, {lineNumber}):");
            if (usages.Count == 1)
            {
                EmitLogToConsoleCall(usages[0]);
            }
            else
            {
                for (int i = 0; i < usages.Count; i++)
                {
                    var usage = usages[i];
                    builder.OpenScope($"if (typeof({usage.Type}) == typeof(T))");
                    EmitLogToConsoleCall(usage);
                    builder.AppendLine("break;");
                    builder.CloseScope();
                }
            }

            builder.AppendLine("break;");
        }
    }
}
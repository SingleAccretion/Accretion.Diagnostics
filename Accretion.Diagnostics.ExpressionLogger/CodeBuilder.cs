using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal class CodeBuilder
    {
        private readonly StringBuilder _builder = new StringBuilder();

        public void OpenScope(string header)
        {
            AppendLine(header);
            AppendLine("{");
        }

        public void CloseScope(string footer = "")
        {
            AppendLine("}" + footer);
        }

        public void AppendLine(string line)
        {
            _builder.AppendLine();
            _builder.Append(line);
        }

        public override string ToString() => _builder.ToString();

        public SourceText ToSourceText() => SourceText.From(ToString(), Encoding.UTF8);
    }
}
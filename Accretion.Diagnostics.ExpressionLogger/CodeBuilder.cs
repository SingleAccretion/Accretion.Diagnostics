using System.Text;

namespace Accretion.Diagnostics.ExpressionLogger
{
    internal struct CodeBuilder
    {
        private StringBuilder _builder;

        private StringBuilder Builder => _builder ??= new StringBuilder();

        public void OpenScope(string header)
        {
            AppendLine(header);
            AppendLine("{");
        }

        public void CloseScope()
        {
            AppendLine("}");
        }

        public void AppendLine(string line)
        {
            Builder.AppendLine();
            Builder.Append(line);
        }

        public override string ToString() => Builder.ToString();
    }
}
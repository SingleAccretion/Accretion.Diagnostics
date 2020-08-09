using System.Text;

namespace Accretion.Diagnostics.FancyDebug
{
    internal struct CodeBuilder
    {
        public const string Indent = "    ";

        private StringBuilder _builder;
        private string _indent;

        private StringBuilder Builder => _builder ??= new StringBuilder();

        public void OpenScope(string header)
        {
            AppendLine(header);
            AppendLine("{");
            _indent += Indent;
        }

        public void CloseScope()
        {
            if (_indent.Length >= Indent.Length)
            {
                _indent = _indent.Substring(0, _indent.Length - Indent.Length);
            }
            AppendLine("}");
        }

        public void AppendLine(string line)
        {
            Builder.AppendLine(_indent);
            Builder.Append(line);
        }

        public override string ToString() => Builder.ToString();
    }
}
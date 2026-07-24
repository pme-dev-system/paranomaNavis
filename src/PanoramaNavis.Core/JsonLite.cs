using System.Globalization;
using System.Text;

namespace PanoramaNavis.Core
{
    /// <summary>
    /// metadata.json 出力専用の最小JSONライタ。
    /// netstandard2.0 で外部パッケージ依存を持たないために自前実装している
    /// （アドイン側のNuGet依存はNavisworksのプラグイン読み込みと衝突しやすい）。
    /// </summary>
    public sealed class JsonLite
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private bool _needComma;
        private int _depth;

        public void BeginObject()
        {
            AppendCommaIfNeeded();
            _sb.Append("{\n");
            _depth++;
            _needComma = false;
        }

        public void EndObject()
        {
            _depth--;
            _sb.Append('\n');
            Indent();
            _sb.Append('}');
            _needComma = true;
        }

        public void Property(string name, string value)
        {
            WriteName(name);
            WriteString(value);
        }

        public void Property(string name, int value)
        {
            WriteName(name);
            _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Property(string name, double value)
        {
            WriteName(name);
            _sb.Append(FormatDouble(value));
        }

        public void PropertyVec3(string name, Vec3 v)
        {
            WriteName(name);
            _sb.Append("{ \"x\": ").Append(FormatDouble(v.X))
               .Append(", \"y\": ").Append(FormatDouble(v.Y))
               .Append(", \"z\": ").Append(FormatDouble(v.Z))
               .Append(" }");
        }

        private static string FormatDouble(double value)
        {
            string s = value.ToString("R", CultureInfo.InvariantCulture);
            // 整数値でもJSON上はdoubleと分かるよう小数点を付ける
            return (s.IndexOf('.') < 0 && s.IndexOf('E') < 0 && s.IndexOf('e') < 0) ? s + ".0" : s;
        }

        private void WriteName(string name)
        {
            AppendCommaIfNeeded();
            Indent();
            WriteString(name);
            _sb.Append(": ");
            _needComma = false;
        }

        private void WriteString(string value)
        {
            _sb.Append('"');
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
            _needComma = true;
        }

        private void AppendCommaIfNeeded()
        {
            if (_needComma)
                _sb.Append(",\n");
        }

        private void Indent()
        {
            for (int i = 0; i < _depth; i++)
                _sb.Append("  ");
        }

        public override string ToString() => _sb.ToString();
    }
}

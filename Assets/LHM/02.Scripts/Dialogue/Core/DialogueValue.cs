using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace RealSteel.Dialogue
{
    /// <summary>조건/효과에서 다루는 변수 타입. 레지스트리(variables.json)에 선언된 타입만 허용된다.</summary>
    public enum VarType { Bool, Int, Float, String }

    /// <summary>
    /// 대화 시스템 전용 값 타입. JToken을 런타임까지 끌고 다니지 않도록
    /// 로드 시점에 한 번 변환(컴파일)해 두고 평가에는 이 구조체만 사용한다.
    /// </summary>
    public readonly struct DVal : IEquatable<DVal>
    {
        public readonly VarType Type;
        private readonly bool _b;
        private readonly double _n;
        private readonly string _s;

        private DVal(VarType type, bool b, double n, string s) { Type = type; _b = b; _n = n; _s = s; }

        public static DVal Bool(bool v) => new DVal(VarType.Bool, v, 0, null);
        public static DVal Int(long v) => new DVal(VarType.Int, false, v, null);
        public static DVal Float(double v) => new DVal(VarType.Float, false, v, null);
        public static DVal Str(string v) => new DVal(VarType.String, false, 0, v ?? string.Empty);

        public bool IsNumeric => Type == VarType.Int || Type == VarType.Float;
        public bool AsBool => _b;
        public double AsNumber => _n;
        public string AsString => _s;

        /// <summary>선언된 타입(expected)에 맞게 JSON 값을 변환. 타입이 다르면 false.</summary>
        public static bool TryFromJToken(JToken token, VarType expected, out DVal value)
        {
            value = default;
            if (token == null || token.Type == JTokenType.Null) return false;
            switch (expected)
            {
                case VarType.Bool:
                    if (token.Type != JTokenType.Boolean) return false;
                    value = Bool(token.Value<bool>());
                    return true;
                case VarType.Int:
                    if (token.Type != JTokenType.Integer) return false;
                    value = Int(token.Value<long>());
                    return true;
                case VarType.Float:
                    if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer) return false;
                    value = Float(token.Value<double>());
                    return true;
                case VarType.String:
                    if (token.Type != JTokenType.String) return false;
                    value = Str(token.Value<string>());
                    return true;
            }
            return false;
        }

        public JToken ToJToken()
        {
            switch (Type)
            {
                case VarType.Bool: return new JValue(_b);
                case VarType.Int: return new JValue((long)_n);
                case VarType.Float: return new JValue(_n);
                default: return new JValue(_s);
            }
        }

        public bool Equals(DVal other)
        {
            if (IsNumeric && other.IsNumeric) return _n.Equals(other._n);
            if (Type != other.Type) return false;
            return Type == VarType.Bool ? _b == other._b : string.Equals(_s, other._s, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is DVal d && Equals(d);

        public override int GetHashCode()
        {
            if (IsNumeric) return _n.GetHashCode();
            return Type == VarType.Bool ? _b.GetHashCode() : (_s?.GetHashCode() ?? 0);
        }

        public override string ToString()
        {
            switch (Type)
            {
                case VarType.Bool: return _b ? "true" : "false";
                case VarType.Int: return ((long)_n).ToString(CultureInfo.InvariantCulture);
                case VarType.Float: return _n.ToString(CultureInfo.InvariantCulture);
                default: return _s;
            }
        }
    }
}

﻿using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ExpressionToCodeLib.Internal
{
    sealed class ObjectStringifyImpl : IObjectStringifier
    {
        readonly bool fullTypeNames;

        public ObjectStringifyImpl(bool fullTypeNames = false)
            => this.fullTypeNames = fullTypeNames;

        public string TypeNameToCode(Type type)
            => new CSharpFriendlyTypeName { UseFullName = fullTypeNames }.GetTypeName(type);

        public string? PlainObjectToCode(object? val, Type? type)
            => val switch {
                null when type == null || type == typeof(object) => "null",
                null => "default(" + TypeNameToCode(type) + ")",
                string str => PreferLiteralSyntax(str) ? "@\"" + str.Replace("\"", "\"\"") + "\"" : "\"" + EscapeStringChars(str) + "\"",
                char charVal => "'" + EscapeCharForString(charVal) + "'",
                decimal _ => Convert.ToString(val, CultureInfo.InvariantCulture) + "m",
                float floatVal => FloatToCode(floatVal),
                double doubleVal => DoubleToCode(doubleVal),
                byte byteVal => "((byte)" + byteVal + ")",
                sbyte sbyteVal => "((sbyte)" + sbyteVal + ")",
                short shortVal => "((short)" + shortVal + ")",
                ushort ushortVal => "((ushort)" + ushortVal + ")",
                int intVal => intVal.ToString(),
                uint uintVal => uintVal + "U",
                long longVal => longVal + "L",
                ulong ulongVal => ulongVal + "UL",
                bool boolVal => boolVal ? "true" : "false",
                Enum enumVal => EnumValueToCode(val, enumVal),
                Type typeVal => "typeof(" + TypeNameToCode(typeVal) + ")",
                MethodInfo methodInfoVal => TypeNameToCode(methodInfoVal.DeclaringType) + "." + methodInfoVal.Name,
                _ when val is ValueType && Activator.CreateInstance(val.GetType()).Equals(val) => "default(" + TypeNameToCode(val.GetType()) + ")",
                _ => null
            };

        string EnumValueToCode(object val, Enum enumVal)
        {
            if (Enum.IsDefined(enumVal.GetType(), enumVal)) {
                return TypeNameToCode(enumVal.GetType()) + "." + enumVal;
            } else {
                var enumAsLong = ((IConvertible)enumVal).ToInt64(null);
                var toString = enumVal.ToString();
                if (toString == enumAsLong.ToString()) {
                    return "((" + TypeNameToCode(enumVal.GetType()) + ")" + enumAsLong + ")";
                } else {
                    var components = toString.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                    return components.Length == 0
                        ? "default(" + TypeNameToCode(enumVal.GetType()) + ")"
                        : components.Length == 1
                            ? TypeNameToCode(enumVal.GetType()) + "." + components[0]
                            : "(" + string.Join(" | ", components.Select(s => TypeNameToCode(val.GetType()) + "." + s)) + ")";
                }
            }
        }

        internal static bool PreferLiteralSyntax(string str1)
        {
            var count = 0;
            foreach (var c in str1) {
                if (c < 32 && c != '\r' || c == '\\') {
                    count++;
                    if (count > 3) {
                        return true;
                    }
                }
            }

            return false;
        }

        static string EscapeCharForString(char c)
        {
            if (c < 32 || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Control) {
                //this is a little too rigorous; but easier to read
                if (c == '\r') {
                    return "\\r";
                } else if (c == '\t') {
                    return "\\t";
                } else if (c == '\n') {
                    return "\\n";
                } else {
                    return "\\x" + Convert.ToString(c, 16);
                }
            } else if (c == '\\') {
                return @"\\";
            } else if (c == '\"') {
                return "\\\"";
            } else {
                return c.ToString();
            }
        }

        internal static string EscapeStringChars(string str)
        {
            var sb = new StringBuilder(str.Length);
            foreach (var c in str) {
                sb.Append(EscapeCharForString(c));
            }

            return sb.ToString();
        }

        static string DoubleToCode(double p)
        {
            if (double.IsNaN(p)) {
                return "double.NaN";
            } else if (double.IsNegativeInfinity(p)) {
                return "double.NegativeInfinity";
            } else if (double.IsPositiveInfinity(p)) {
                return "double.PositiveInfinity";
            } else if (Math.Abs(p) > uint.MaxValue) {
                return p.ToString("0.0########################e0", CultureInfo.InvariantCulture);
            } else {
                return p.ToString("0.0########################", CultureInfo.InvariantCulture);
            }
        }

        static string FloatToCode(float p)
        {
            if (float.IsNaN(p)) {
                return "float.NaN";
            } else if (float.IsNegativeInfinity(p)) {
                return "float.NegativeInfinity";
            } else if (float.IsPositiveInfinity(p)) {
                return "float.PositiveInfinity";
            } else if (Math.Abs(p) >= 1 << 24) {
                return p.ToString("0.0########e0", CultureInfo.InvariantCulture) + "f";
            } else {
                return p.ToString("0.0########", CultureInfo.InvariantCulture) + "f";
            }
        }
    }
}

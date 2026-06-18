// Json — the serde_json analogue for this port.
//
// The Rust host leaned on serde_json::Value for all of its dynamic JSON: parsing config,
// building Ollama request bodies, and reading JSON-RPC. .NET Framework ships its own JSON
// serializer (JavaScriptSerializer, in System.Web.Extensions.dll), so we use that instead of
// pulling in an external package — same "lean, use the platform" ethos as the Rust side using
// std only. This file wraps it and adds the small navigation helpers (GetString, GetArr, ...)
// that replace serde_json's Value accessors.
//
// Shape of a parsed value (DeserializeObject): JSON object -> Dictionary<string, object>,
// JSON array -> object[], numbers -> int/long/decimal, strings/bools/null as-is.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace Icm
{
    internal static class Json
    {
        private static JavaScriptSerializer NewSerializer()
        {
            var s = new JavaScriptSerializer();
            // KB answers and TSV payloads can be large; the 2 MB default would truncate them.
            s.MaxJsonLength = int.MaxValue;
            return s;
        }

        public static string Serialize(object value)
        {
            return NewSerializer().Serialize(value);
        }

        // Serialize with 2-space indentation, for human-readable generated files (e.g. manifest.json).
        public static string SerializePretty(object value)
        {
            return Pretty(Serialize(value));
        }

        // Re-indent compact JSON to 2-space pretty form. Also unescapes the \uXXXX sequences that
        // JavaScriptSerializer emits for printable characters (it escapes <, >, ', & for HTML safety),
        // which we do not need in a config file. Structural-only: it tracks string literals so braces
        // inside strings are left alone.
        public static string Pretty(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            var sb = new System.Text.StringBuilder(json.Length + 64);
            int indent = 0;
            bool inStr = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inStr)
                {
                    if (c == '\\' && i + 1 < json.Length)
                    {
                        char n = json[i + 1];
                        if (n == 'u' && i + 5 < json.Length)
                        {
                            int code;
                            if (int.TryParse(json.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)
                                && code >= 0x20 && code != 0x22 && code != 0x5C && code != 0x7F)
                            {
                                sb.Append((char)code); i += 5; continue;
                            }
                        }
                        sb.Append(c); sb.Append(n); i += 1; continue;
                    }
                    if (c == '"') inStr = false;
                    sb.Append(c);
                    continue;
                }
                switch (c)
                {
                    case '"': inStr = true; sb.Append(c); break;
                    case '{':
                    case '[':
                    {
                        char close = (c == '{') ? '}' : ']';
                        int j = i + 1;
                        while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
                        if (j < json.Length && json[j] == close) { sb.Append(c).Append(close); i = j; } // empty {} or []
                        else { sb.Append(c); indent++; NewlineIndent(sb, indent); }
                        break;
                    }
                    case '}':
                    case ']': indent--; NewlineIndent(sb, indent); sb.Append(c); break;
                    case ',': sb.Append(c); NewlineIndent(sb, indent); break;
                    case ':': sb.Append(": "); break;
                    default: if (!char.IsWhiteSpace(c)) sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static void NewlineIndent(System.Text.StringBuilder sb, int indent)
        {
            sb.Append('\n');
            for (int i = 0; i < indent; i++) sb.Append("  ");
        }

        /// Parse arbitrary JSON text into the dynamic object graph described above.
        public static object Parse(string text)
        {
            return NewSerializer().DeserializeObject(text);
        }

        // --- builders: construct JSON objects / grammar schemas for Ollama and JSON-RPC ---

        // Build an object from alternating key, value, key, value ... arguments.
        public static Dictionary<string, object> Obj(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        // A {"type":"string"} property.
        public static Dictionary<string, object> StrProp() { return Obj("type", "string"); }

        // A {"type":"string","enum":[...]} property.
        public static Dictionary<string, object> EnumProp(IEnumerable<string> values)
        {
            var arr = new List<object>();
            foreach (string v in values) arr.Add(v);
            return Obj("type", "string", "enum", arr.ToArray());
        }

        // A JSON-schema object: {"type":"object","properties":{...},"required":[...]}.
        public static Dictionary<string, object> Schema(Dictionary<string, object> properties, params string[] required)
        {
            var req = new List<object>();
            foreach (string r in required) req.Add(r);
            return Obj("type", "object", "properties", properties, "required", req.ToArray());
        }

        // --- navigation: the serde_json Value::get / as_str / as_array analogues ---

        public static Dictionary<string, object> AsObject(object value)
        {
            return value as Dictionary<string, object>;
        }

        /// A child object at key, or null. The .get(k) then as_object move.
        public static Dictionary<string, object> GetObject(Dictionary<string, object> obj, string key)
        {
            object v;
            if (obj != null && obj.TryGetValue(key, out v)) return v as Dictionary<string, object>;
            return null;
        }

        public static string GetString(Dictionary<string, object> obj, string key)
        {
            object v;
            if (obj != null && obj.TryGetValue(key, out v) && v != null) return v as string;
            return null;
        }

        public static string GetStringOr(Dictionary<string, object> obj, string key, string fallback)
        {
            string s = GetString(obj, key);
            return s != null ? s : fallback;
        }

        public static bool GetBool(Dictionary<string, object> obj, string key, bool fallback)
        {
            object v;
            if (obj != null && obj.TryGetValue(key, out v) && v is bool) return (bool)v;
            return fallback;
        }

        /// A numeric field as double, or null when absent / non-numeric. Tolerates the several
        /// CLR numeric types JavaScriptSerializer may hand back (int, long, decimal, double).
        public static double? GetNumber(Dictionary<string, object> obj, string key)
        {
            object v;
            if (obj == null || !obj.TryGetValue(key, out v) || v == null) return null;
            return ToDouble(v);
        }

        public static double? ToDouble(object v)
        {
            if (v == null) return null;
            if (v is double) return (double)v;
            if (v is int) return (int)v;
            if (v is long) return (long)v;
            if (v is decimal) return (double)(decimal)v;
            if (v is float) return (float)v;
            double d;
            string s = v as string;
            if (s != null && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
            return null;
        }

        /// An array field as a List, or an empty list. Replaces as_array().
        public static List<object> GetArr(Dictionary<string, object> obj, string key)
        {
            object v;
            if (obj != null && obj.TryGetValue(key, out v)) return AsArr(v);
            return new List<object>();
        }

        public static List<object> AsArr(object v)
        {
            var list = new List<object>();
            var arr = v as object[];
            if (arr != null) { list.AddRange(arr); return list; }
            var already = v as List<object>;
            if (already != null) return already;
            return list;
        }

        /// Navigate a slash path like "/params/name" through nested objects. The serde_json
        /// Value::pointer analogue used by the MCP handler.
        public static object Pointer(object root, string pointer)
        {
            if (string.IsNullOrEmpty(pointer)) return root;
            object cur = root;
            string[] parts = pointer.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i];
                if (token.Length == 0) continue; // leading empty from the first '/'
                var obj = cur as Dictionary<string, object>;
                if (obj == null) return null;
                if (!obj.TryGetValue(token, out cur)) return null;
            }
            return cur;
        }
    }
}

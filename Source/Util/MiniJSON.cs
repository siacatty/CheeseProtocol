// MiniJSON.cs (public domain-style minimal JSON parser)
// Based on MiniJSON used widely in Unity projects.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Verse;

namespace CheeseProtocol
{
    internal static class MiniJSON
    {
        public static object Deserialize(string json)
        {
            if (json == null) {
                Log.Warning("[CheeseProtocol] MiniJSON.Deserialize called with null json");
                return null;
            }
            return Parser.Parse(json);
        }

        public static string Serialize(object obj)
        {
            var sb = new StringBuilder(256);
            SerializeValue(obj, sb);
            return sb.ToString();
        }

        private static void SerializeValue(object value, StringBuilder sb)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            switch (value)
            {
                case string s:
                    SerializeString(s, sb);
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case IDictionary dict:
                    SerializeObject(dict, sb);
                    return;
                case IList list:
                    SerializeArray(list, sb);
                    return;
                case float f:
                    sb.Append(f.ToString(CultureInfo.InvariantCulture));
                    return;
                case double d:
                    sb.Append(d.ToString(CultureInfo.InvariantCulture));
                    return;
                case int i:
                    sb.Append(i);
                    return;
                case long l:
                    sb.Append(l);
                    return;
                default:
                    // fallback: ToString as JSON string
                    SerializeString(value.ToString(), sb);
                    return;
            }
        }

        private static void SerializeObject(IDictionary obj, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            foreach (DictionaryEntry e in obj)
            {
                if (!first) sb.Append(',');
                first = false;

                SerializeString(e.Key.ToString(), sb);
                sb.Append(':');
                SerializeValue(e.Value, sb);
            }
            sb.Append('}');
        }

        private static void SerializeArray(IList arr, StringBuilder sb)
        {
            bool first = true;
            sb.Append('[');
            foreach (var v in arr)
            {
                if (!first) sb.Append(',');
                first = false;
                SerializeValue(v, sb);
            }
            sb.Append(']');
        }

        private static void SerializeString(string s, StringBuilder sb)
        {
            sb.Append('\"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('\"');
        }

        private sealed class Parser : IDisposable
        {
            private const string WORD_BREAK = "{}[],:\"";
            private StringReader json;

            private Parser(string jsonString) { json = new StringReader(jsonString); }

            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString))
                    return instance.ParseValue();
            }

            public void Dispose() { json.Dispose(); json = null; }

            private Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();

                // consume '{'
                json.Read();

                while (true)
                {
                    var nextToken = NextToken;
                    if (nextToken == TOKEN.NONE) {
                        //Log.Warning("[CheeseProtocol] nextToken is None");
                        return null;
                    }
                    // }
                    if (nextToken == TOKEN.CURLY_CLOSE) {
                        //Log.Warning("[CheeseProtocol] nextToken is curlyClose");
                        return table;
                    }
                    // ,
                    if (nextToken == TOKEN.COMMA)
                    {
                        json.Read(); // consume ','
                        continue;
                    }
                    // key
                    string name = ParseString();
                    //Log.Warning("[CheeseProtocol] MiniJSON key: " + name);
                    if (name == null) {
                        //Log.Warning("[CheeseProtocol] key is null");
                        return null;
                    }

                    // ':'
                    var tok = NextToken;
                    if (tok != TOKEN.COLON){
                        //Log.Warning("[CheeseProtocol] no colon found.");
                        return null;
                    }
                    json.Read();
                    // value
                    table[name] = ParseValue();
                }
            }

            private List<object> ParseArray()
            {
                var array = new List<object>();

                // consume '['
                json.Read();

                var parsing = true;
                while (parsing)
                {
                    var nextToken = NextToken;
                    if (nextToken == TOKEN.NONE) {
                        return null;
                    }

                    if (nextToken == TOKEN.SQUARE_CLOSE)
                    {
                        json.Read();
                        break;
                    }

                    object value = ParseValue();
                    array.Add(value);

                    nextToken = NextToken;
                    if (nextToken == TOKEN.COMMA)
                        json.Read();
                    else if (nextToken == TOKEN.SQUARE_CLOSE)
                        continue;
                    else if (nextToken == TOKEN.NONE){
                        return null;
                    }
                }

                return array;
            }

            private object ParseValue()
            {
                switch (NextToken)
                {
                    case TOKEN.STRING: return ParseString();
                    case TOKEN.NUMBER: return ParseNumber();
                    case TOKEN.CURLY_OPEN: return ParseObject();
                    case TOKEN.SQUARE_OPEN: return ParseArray();
                    case TOKEN.TRUE: return true;
                    case TOKEN.FALSE: return false;
                    case TOKEN.NULL: return null;
                    default: return null;
                    
                }
            }

            private string ParseString()
            {
                var s = new StringBuilder();
                char c;

                // consume '"'
                json.Read();

                var parsing = true;
                while (parsing)
                {
                    if (json.Peek() == -1) break;

                    c = NextChar;
                    switch (c)
                    {
                        case '"': parsing = false; break;
                        case '\\':
                            if (json.Peek() == -1) parsing = false;
                            else
                            {
                                c = NextChar;
                                switch (c)
                                {
                                    case '"': s.Append('"'); break;
                                    case '\\': s.Append('\\'); break;
                                    case '/': s.Append('/'); break;
                                    case 'b': s.Append('\b'); break;
                                    case 'f': s.Append('\f'); break;
                                    case 'n': s.Append('\n'); break;
                                    case 'r': s.Append('\r'); break;
                                    case 't': s.Append('\t'); break;
                                    case 'u':
                                        var hex = new char[4];
                                        for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                        s.Append((char)Convert.ToInt32(new string(hex), 16));
                                        break;
                                }
                            }
                            break;
                        default:
                            s.Append(c);
                            break;
                    }
                }

                return s.ToString();
            }

            private object ParseNumber()
            {
                string number = NextWord;

                if (number.IndexOf('.') == -1)
                {
                    if (long.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedInt))
                        return parsedInt;
                }

                if (double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDouble))
                    return parsedDouble;

                return 0;
            }

            private void EatWhitespace()
            {
                while (Char.IsWhiteSpace(PeekChar))
                {
                    json.Read();
                    if (json.Peek() == -1) break;
                }
            }

            private char PeekChar => Convert.ToChar(json.Peek());
            private char NextChar => Convert.ToChar(json.Read());

            private string NextWord
            {
                get
                {
                    var word = new StringBuilder();
                    while (json.Peek() != -1 && WORD_BREAK.IndexOf(PeekChar) == -1 && !Char.IsWhiteSpace(PeekChar))
                        word.Append(NextChar);
                    return word.ToString();
                }
            }

            private TOKEN NextToken
            {
                get
                {
                    EatWhitespace();
                    if (json.Peek() == -1) return TOKEN.NONE;

                    char c = PeekChar;
                    //Log.Warning("[CheeseProtocol] TEST next character: " + c);
                    switch (c)
                    {
                        case '{': return TOKEN.CURLY_OPEN;
                        case '}': json.Read(); return TOKEN.CURLY_CLOSE;
                        case '[': return TOKEN.SQUARE_OPEN;
                        case ']': return TOKEN.SQUARE_CLOSE;
                        case ',': return TOKEN.COMMA;
                        case '"': return TOKEN.STRING;
                        case ':': return TOKEN.COLON;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                        case '-': return TOKEN.NUMBER;
                    }

                    string word = NextWord;
                    switch (word)
                    {
                        case "false": return TOKEN.FALSE;
                        case "true": return TOKEN.TRUE;
                        case "null": return TOKEN.NULL;
                    }

                    return TOKEN.NONE;
                }
            }

            private enum TOKEN
            {
                NONE, CURLY_OPEN, CURLY_CLOSE, SQUARE_OPEN, SQUARE_CLOSE,
                COLON, COMMA, STRING, NUMBER, TRUE, FALSE, NULL
            }
        }
    }
}

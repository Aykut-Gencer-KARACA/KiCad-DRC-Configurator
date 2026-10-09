using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KiCadDrc
{
    // Small S-expression helpers (KiCad files). Parentheses inside strings are skipped.
    static class Sexp
    {
        public static int Closing(string t, int open)
        {
            int depth = 0;
            for (int i = open; i < t.Length; i++)
            {
                char c = t[i];
                if (c == '"') { i = StringEnd(t, i); continue; }
                if (c == '(') depth++;
                else if (c == ')' && --depth == 0) return i;
            }
            throw new InvalidOperationException(Tx.KiCadFileBroken);
        }

        static int StringEnd(string t, int i)
        {
            for (i++; i < t.Length; i++)
            {
                if (t[i] == '\\') i++;
                else if (t[i] == '"') return i;
            }
            return t.Length - 1;
        }

        public class Node
        {
            public string Name; public int Start, End;
            public string Text(string t) { return t.Substring(Start, End - Start + 1); }
        }

        // Direct child nodes of the [start, end] node
        public static List<Node> Children(string t, int start, int end)
        {
            var l = new List<Node>();
            for (int i = start + 1; i < end; i++)
            {
                char c = t[i];
                if (c == '"') { i = StringEnd(t, i); continue; }
                if (c != '(') continue;
                int k = Closing(t, i), j = i + 1;
                while (j < k && !char.IsWhiteSpace(t[j]) && t[j] != '(' && t[j] != ')') j++;
                l.Add(new Node { Name = t.Substring(i + 1, j - i - 1), Start = i, End = k });
                i = k;
            }
            return l;
        }

        public static List<Node> Children(string t, Node n) { return Children(t, n.Start, n.End); }

        public static Node Root(string t, string name)
        {
            int i = t.IndexOf("(" + name, StringComparison.Ordinal);
            if (i < 0) throw new InvalidOperationException(Tx.NotAKiCadFile(name));
            return new Node { Name = name, Start = i, End = Closing(t, i) };
        }
    }
}

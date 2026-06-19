// ChatMarkdown - renders parsed markdown (Markdown.Parse) into a RichTextBox using per-run selection
// formatting. GUI-only (build.ps1 keeps it out of the console exe). Prose is drawn in a proportional
// font, code in Consolas with a distinct background, headings larger/bold. The parse itself lives in
// Runtime/Markdown.cs so it can be unit-tested without WinForms.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Icm
{
    internal static class ChatMarkdown
    {
        private const string ProseFamily = "Segoe UI";
        private const float ProseSize = 10f;
        private const string CodeFamily = "Consolas";
        private const float CodeSize = 9.75f;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_SETREDRAW = 0x000B;

        private static readonly Dictionary<string, Font> fontCache = new Dictionary<string, Font>();

        public static Font GetFont(string family, float size, FontStyle style)
        {
            string key = family + "|" + size + "|" + (int)style;
            Font f;
            if (!fontCache.TryGetValue(key, out f)) { f = new Font(family, size, style); fontCache[key] = f; }
            return f;
        }

        // Render markdown to the end of the RichTextBox. Redraw is frozen during the append to avoid
        // flicker on long code blocks.
        public static void Render(RichTextBox rtb, string md)
        {
            bool froze = false;
            try
            {
                if (rtb.IsHandleCreated) { SendMessage(rtb.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero); froze = true; }
                RenderLines(rtb, Markdown.Parse(md));
            }
            finally
            {
                if (froze) { SendMessage(rtb.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero); rtb.Invalidate(); }
                rtb.SelectionStart = rtb.TextLength; rtb.SelectionLength = 0;
                rtb.ScrollToCaret();
            }
        }

        private static void RenderLines(RichTextBox rtb, List<MdLine> lines)
        {
            foreach (MdLine ln in lines)
            {
                switch (ln.Kind)
                {
                    case MdLineKind.Blank:
                        Append(rtb, "\n", Prose(FontStyle.Regular), Theme.Fg, Theme.Bg);
                        break;
                    case MdLineKind.Code:
                        Append(rtb, "  " + ln.Raw + "\n", GetFont(CodeFamily, CodeSize, FontStyle.Regular), Theme.CodeFg, Theme.CodeBg);
                        break;
                    case MdLineKind.Rule:
                        Append(rtb, new string('─', 28) + "\n", Prose(FontStyle.Regular), Theme.FgDim, Theme.Bg);
                        break;
                    case MdLineKind.Heading:
                        Append(rtb, HeadingText(ln) + "\n", GetFont(ProseFamily, HeadingSize(ln.Level), FontStyle.Bold), Theme.Heading, Theme.Bg);
                        break;
                    case MdLineKind.Bullet:
                        Append(rtb, Indent(ln.Level) + "•  ", Prose(FontStyle.Regular), Theme.Accent, Theme.Bg);
                        EmitSpans(rtb, ln.Spans, ProseFamily, ProseSize, Theme.Fg);
                        Append(rtb, "\n", Prose(FontStyle.Regular), Theme.Fg, Theme.Bg);
                        break;
                    case MdLineKind.Ordered:
                        Append(rtb, Indent(ln.Level) + ln.Marker + ".  ", Prose(FontStyle.Regular), Theme.Accent, Theme.Bg);
                        EmitSpans(rtb, ln.Spans, ProseFamily, ProseSize, Theme.Fg);
                        Append(rtb, "\n", Prose(FontStyle.Regular), Theme.Fg, Theme.Bg);
                        break;
                    case MdLineKind.Quote:
                        Append(rtb, "  │ ", Prose(FontStyle.Regular), Theme.FgDim, Theme.Bg);
                        EmitSpans(rtb, ln.Spans, ProseFamily, ProseSize, Theme.FgDim);
                        Append(rtb, "\n", Prose(FontStyle.Regular), Theme.Fg, Theme.Bg);
                        break;
                    default: // Paragraph
                        EmitSpans(rtb, ln.Spans, ProseFamily, ProseSize, Theme.Fg);
                        Append(rtb, "\n", Prose(FontStyle.Regular), Theme.Fg, Theme.Bg);
                        break;
                }
            }
        }

        private static void EmitSpans(RichTextBox rtb, List<MdSpan> spans, string fam, float size, Color baseColor)
        {
            foreach (MdSpan sp in spans)
            {
                switch (sp.Style)
                {
                    case MdSpanStyle.Code:
                        Append(rtb, sp.Text, GetFont(CodeFamily, CodeSize, FontStyle.Regular), Theme.CodeFg, Theme.CodeBg);
                        break;
                    case MdSpanStyle.Bold:
                        Append(rtb, sp.Text, GetFont(fam, size, FontStyle.Bold), baseColor, Theme.Bg);
                        break;
                    case MdSpanStyle.Italic:
                        Append(rtb, sp.Text, GetFont(fam, size, FontStyle.Italic), baseColor, Theme.Bg);
                        break;
                    case MdSpanStyle.Link:
                        Append(rtb, sp.Text, GetFont(fam, size, FontStyle.Underline), Theme.Accent, Theme.Bg);
                        break;
                    default:
                        Append(rtb, sp.Text, GetFont(fam, size, FontStyle.Regular), baseColor, Theme.Bg);
                        break;
                }
            }
        }

        private static Font Prose(FontStyle style) { return GetFont(ProseFamily, ProseSize, style); }
        private static string Indent(int level) { return new string(' ', 2 + (level > 0 ? level * 2 : 0)); }
        private static float HeadingSize(int level) { return level <= 1 ? 15f : level == 2 ? 13f : level == 3 ? 11.5f : 10.5f; }
        private static string HeadingText(MdLine ln)
        {
            var sb = new System.Text.StringBuilder();
            foreach (MdSpan sp in ln.Spans) sb.Append(sp.Text);
            return sb.ToString();
        }

        private static void Append(RichTextBox rtb, string text, Font font, Color fore, Color back)
        {
            if (string.IsNullOrEmpty(text)) return;
            rtb.SelectionStart = rtb.TextLength; rtb.SelectionLength = 0;
            rtb.SelectionFont = font;
            rtb.SelectionColor = fore;
            rtb.SelectionBackColor = back;
            rtb.AppendText(text);
        }
    }
}

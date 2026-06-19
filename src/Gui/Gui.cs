// icm-gui - a "VSCode lite" WinForms front end for the ICM host.
//
// Built with the in-box .NET Framework csc.exe (no SDK): a native, dark-themed window with a
// workspace file tree (add/delete/rename/read/edit, confined to the opened root), a text editor
// with a line-number gutter and find/replace, and a chat panel that drives the SAME Dispatcher
// the console uses. When the opened folder is an ICM instance (has icm.config.json) the chat
// lights up: conversation -> dispatcher -> ICM agent + oracle, with the Ollama reachability shown
// as a dot and a Validate-File button that runs the oracle on the editor buffer.
//
// Threading: model calls block, so each chat turn runs on a worker thread; the Dispatcher's
// status trace and the final result are marshalled back to the UI thread via BeginInvoke. The
// Cancel button aborts the in-flight request through the Dispatcher's Cancel handle.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Icm
{
    internal static class GuiMain
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ToolStripManager.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
            string initial = args.Length > 0 ? args[0] : null;
            Application.Run(new MainForm(initial));
        }
    }

    // The palette. One place to retune the dark theme.
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(30, 30, 30);
        public static readonly Color Panel = Color.FromArgb(37, 37, 38);
        public static readonly Color Panel2 = Color.FromArgb(51, 51, 55);
        public static readonly Color Fg = Color.Gainsboro;
        public static readonly Color FgDim = Color.FromArgb(140, 140, 140);
        public static readonly Color Accent = Color.FromArgb(0, 122, 204);
        public static readonly Color Gutter = Color.FromArgb(40, 40, 42);
        public static readonly Color Good = Color.FromArgb(106, 191, 105);
        public static readonly Color Bad = Color.FromArgb(240, 113, 100);
        public static readonly Color CodeBg = Color.FromArgb(45, 45, 48);   // chat code-block background
        public static readonly Color CodeFg = Color.FromArgb(215, 200, 160); // chat code text
        public static readonly Color Heading = Color.FromArgb(236, 236, 236);// chat heading text
    }

    // Darkens menus, toolbars, and the status strip (the ProfessionalRenderer reads this).
    internal class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin { get { return Theme.Panel; } }
        public override Color ToolStripGradientMiddle { get { return Theme.Panel; } }
        public override Color ToolStripGradientEnd { get { return Theme.Panel; } }
        public override Color ToolStripBorder { get { return Theme.Panel; } }
        public override Color MenuStripGradientBegin { get { return Theme.Panel; } }
        public override Color MenuStripGradientEnd { get { return Theme.Panel; } }
        public override Color StatusStripGradientBegin { get { return Theme.Panel; } }
        public override Color StatusStripGradientEnd { get { return Theme.Panel; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Panel; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Panel; } }
        public override Color MenuItemSelected { get { return Theme.Panel2; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Panel2; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Panel2; } }
        public override Color MenuItemBorder { get { return Theme.Accent; } }
        public override Color MenuBorder { get { return Theme.Panel2; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
        public override Color ButtonSelectedGradientBegin { get { return Theme.Panel2; } }
        public override Color ButtonSelectedGradientMiddle { get { return Theme.Panel2; } }
        public override Color ButtonSelectedGradientEnd { get { return Theme.Panel2; } }
        public override Color ButtonSelectedBorder { get { return Theme.Accent; } }
        public override Color ButtonPressedGradientBegin { get { return Theme.Accent; } }
        public override Color ButtonPressedGradientMiddle { get { return Theme.Accent; } }
        public override Color ButtonPressedGradientEnd { get { return Theme.Accent; } }
        public override Color SeparatorDark { get { return Theme.Panel2; } }
        public override Color SeparatorLight { get { return Theme.Panel2; } }
    }

    internal class MainForm : Form
    {
        // workspace
        private string root;
        private TreeView tree;
        private RichTextBox editor;
        private Panel gutter;
        private string currentFile;
        private bool dirty;
        private bool loading;
        private IconProvider icons;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel caretLabel;
        private SplitContainer outer;
        private SplitContainer inner;
        private FindDialog findDialog;
        private ToolStripMenuItem wrapItem;

        // chat / ICM
        private RichTextBox chatLog;
        private TextBox chatInput;
        private Button chatSend;
        private Label icmBanner;
        private Label ollamaDot;
        private Dispatcher dispatcher;
        private string ollamaUrl;
        private bool turnRunning;
        private bool turnCancelled;
        private System.Windows.Forms.Timer thinkTimer;
        private int thinkTick;

        public MainForm(string initialRoot)
        {
            Text = "icm-gui - ICM host";
            Width = 1180; Height = 760;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg; ForeColor = Theme.Fg;
            KeyPreview = true;
            BuildUi();
            if (!string.IsNullOrEmpty(initialRoot))
            {
                try { string full = Path.GetFullPath(initialRoot); if (Directory.Exists(full)) OpenWorkspace(full); }
                catch { }
            }
            if (root == null) SetStatus("Open a folder to begin (File > Open Folder).");
        }

        // ----- UI construction -----

        private void BuildUi()
        {
            var menu = new MenuStrip();
            var mFile = new ToolStripMenuItem("File");
            mFile.DropDownItems.Add(new ToolStripMenuItem("Open Folder...", null, delegate { DoOpenFolder(); }, Keys.Control | Keys.O));
            mFile.DropDownItems.Add(new ToolStripMenuItem("Quick Open...", null, delegate { QuickOpen(); }, Keys.Control | Keys.P));
            mFile.DropDownItems.Add(new ToolStripMenuItem("New File...", null, delegate { DoNewFile(); }, Keys.Control | Keys.N));
            mFile.DropDownItems.Add(new ToolStripMenuItem("Save", null, delegate { DoSave(); }, Keys.Control | Keys.S));
            mFile.DropDownItems.Add(new ToolStripMenuItem("Close File", null, delegate { CloseFile(); }, Keys.Control | Keys.W));
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add(new ToolStripMenuItem("Exit", null, delegate { Close(); }));

            var mEdit = new ToolStripMenuItem("Edit");
            mEdit.DropDownItems.Add(new ToolStripMenuItem("Find...", null, delegate { ShowFind(false); }, Keys.Control | Keys.F));
            mEdit.DropDownItems.Add(new ToolStripMenuItem("Replace...", null, delegate { ShowFind(true); }, Keys.Control | Keys.H));
            mEdit.DropDownItems.Add(new ToolStripMenuItem("Go to Line...", null, delegate { GoToLine(); }, Keys.Control | Keys.G));

            var mView = new ToolStripMenuItem("View");
            wrapItem = new ToolStripMenuItem("Word Wrap", null, delegate { ToggleWordWrap(); });
            wrapItem.ShowShortcutKeys = true; wrapItem.ShortcutKeyDisplayString = "Alt+Z"; // handled in ProcessCmdKey
            wrapItem.Checked = false;
            mView.DropDownItems.Add(wrapItem);
            mView.DropDownItems.Add(new ToolStripSeparator());
            var zin = new ToolStripMenuItem("Zoom In", null, delegate { Zoom(0.1f); }, Keys.Control | Keys.Oemplus); zin.ShortcutKeyDisplayString = "Ctrl++";
            var zout = new ToolStripMenuItem("Zoom Out", null, delegate { Zoom(-0.1f); }, Keys.Control | Keys.OemMinus); zout.ShortcutKeyDisplayString = "Ctrl+-";
            var zres = new ToolStripMenuItem("Reset Zoom", null, delegate { ZoomReset(); }, Keys.Control | Keys.D0); zres.ShortcutKeyDisplayString = "Ctrl+0";
            mView.DropDownItems.Add(zin); mView.DropDownItems.Add(zout); mView.DropDownItems.Add(zres);
            mView.DropDownItems.Add(new ToolStripSeparator());
            mView.DropDownItems.Add(new ToolStripMenuItem("Focus Explorer", null, delegate { FocusTree(); }, Keys.Control | Keys.Shift | Keys.E));
            mView.DropDownItems.Add(new ToolStripMenuItem("Focus Chat", null, delegate { FocusChat(); }, Keys.Control | Keys.L));
            mView.DropDownItems.Add(new ToolStripMenuItem("Refresh Tree", null, delegate { RefreshTree(); }, Keys.F5));

            var mIcm = new ToolStripMenuItem("ICM");
            mIcm.DropDownItems.Add(new ToolStripMenuItem("Validate Current File", null, delegate { ValidateCurrentFile(); }, Keys.F8));

            menu.Items.Add(mFile);
            menu.Items.Add(mEdit);
            menu.Items.Add(mView);
            menu.Items.Add(mIcm);
            MainMenuStrip = menu;
            ThemeMenu(menu);

            var toolbar = new ToolStrip();
            toolbar.GripStyle = ToolStripGripStyle.Hidden;
            AddToolButton(toolbar, "Open Folder", delegate { DoOpenFolder(); });
            toolbar.Items.Add(new ToolStripSeparator());
            AddToolButton(toolbar, "New File", delegate { DoNewFile(); });
            AddToolButton(toolbar, "New Folder", delegate { DoNewFolder(); });
            AddToolButton(toolbar, "Rename", delegate { DoRename(); });
            AddToolButton(toolbar, "Delete", delegate { DoDelete(); });
            toolbar.Items.Add(new ToolStripSeparator());
            AddToolButton(toolbar, "Save", delegate { DoSave(); });
            AddToolButton(toolbar, "Find", delegate { ShowFind(false); });
            toolbar.Items.Add(new ToolStripSeparator());
            AddToolButton(toolbar, "Validate File", delegate { ValidateCurrentFile(); });
            AddToolButton(toolbar, "Refresh", delegate { RefreshTree(); });
            ThemeMenu(toolbar);

            var status = new StatusStrip();
            status.BackColor = Theme.Panel; status.ForeColor = Theme.Fg;
            statusLabel = new ToolStripStatusLabel(""); statusLabel.Spring = true; statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            caretLabel = new ToolStripStatusLabel(""); caretLabel.TextAlign = ContentAlignment.MiddleRight;
            status.Items.Add(statusLabel); status.Items.Add(caretLabel);

            outer = new SplitContainer();
            outer.Dock = DockStyle.Fill; outer.Orientation = Orientation.Vertical;
            outer.BackColor = Theme.Panel2;
            // Panel min-sizes and splitter distances are set in OnLoad, once the controls have a
            // real size (setting them here, at the default 150px width, throws).

            tree = new TreeView();
            tree.Dock = DockStyle.Fill; tree.HideSelection = false; tree.BorderStyle = BorderStyle.None;
            tree.BackColor = Theme.Panel; tree.ForeColor = Theme.Fg; tree.LineColor = Theme.FgDim;
            icons = new IconProvider(); tree.ImageList = icons.Images;
            tree.BeforeExpand += OnBeforeExpand;
            tree.NodeMouseDoubleClick += delegate(object s, TreeNodeMouseClickEventArgs e) { OpenNode(e.Node); };
            tree.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete) { DoDelete(); e.Handled = true; }
                else if (e.KeyCode == Keys.F2) { DoRename(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { OpenNode(tree.SelectedNode); e.Handled = true; }
            };
            tree.ContextMenuStrip = BuildTreeContextMenu();
            outer.Panel1.Controls.Add(tree);

            inner = new SplitContainer();
            inner.Dock = DockStyle.Fill; inner.Orientation = Orientation.Vertical;
            inner.BackColor = Theme.Panel2;

            BuildEditor(inner.Panel1);
            inner.Panel2.Controls.Add(BuildChatPanel());
            outer.Panel2.Controls.Add(inner);

            Controls.Add(outer);
            Controls.Add(toolbar);
            Controls.Add(status);
            Controls.Add(menu);
        }

        private void BuildEditor(Control host)
        {
            editor = new RichTextBox();
            editor.Dock = DockStyle.Fill;
            editor.BorderStyle = BorderStyle.None;
            editor.BackColor = Theme.Bg; editor.ForeColor = Theme.Fg;
            editor.Font = new Font("Consolas", 10.5f);
            editor.WordWrap = false; editor.AcceptsTab = true; editor.HideSelection = false;
            editor.MaxLength = 0;
            editor.TextChanged += delegate { if (!loading) { dirty = true; UpdateTitle(); } gutter.Invalidate(); };
            editor.SelectionChanged += delegate { UpdateCaret(); gutter.Invalidate(); };
            editor.VScroll += delegate { gutter.Invalidate(); };
            editor.Resize += delegate { gutter.Invalidate(); };

            gutter = new Panel();
            gutter.Dock = DockStyle.Left; gutter.Width = 50; gutter.BackColor = Theme.Gutter;
            gutter.Paint += delegate(object s, PaintEventArgs e) { PaintGutter(e.Graphics); };

            host.Controls.Add(editor);
            host.Controls.Add(gutter);
            editor.BringToFront();
        }

        private void PaintGutter(Graphics g)
        {
            g.Clear(Theme.Gutter);
            if (editor == null) return;
            int first = editor.GetCharIndexFromPosition(new Point(1, 1));
            int firstLine = editor.GetLineFromCharIndex(first);
            int curLine = editor.GetLineFromCharIndex(editor.SelectionStart);
            Font f = editor.Font;
            for (int line = firstLine; line < firstLine + 6000; line++)
            {
                int idx = editor.GetFirstCharIndexFromLine(line);
                bool last = idx < 0;
                Point p = editor.GetPositionFromCharIndex(last ? Math.Max(0, editor.TextLength) : idx);
                if (p.Y > gutter.Height) break;
                Color c = (line == curLine) ? Theme.Fg : Theme.FgDim;
                TextRenderer.DrawText(g, (line + 1).ToString(), f,
                    new Rectangle(0, p.Y, gutter.Width - 6, f.Height + 2), c,
                    TextFormatFlags.Right | TextFormatFlags.NoPadding);
                if (last) break;
            }
        }

        private void AddToolButton(ToolStrip bar, string text, EventHandler onClick)
        {
            var b = new ToolStripButton(text);
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            b.ForeColor = Theme.Fg;
            b.Click += onClick;
            bar.Items.Add(b);
        }

        private static void ThemeMenu(ToolStrip strip)
        {
            strip.BackColor = Theme.Panel; strip.ForeColor = Theme.Fg;
            foreach (ToolStripItem it in strip.Items) ThemeItem(it);
        }

        private static void ThemeItem(ToolStripItem it)
        {
            it.ForeColor = Theme.Fg;
            var mi = it as ToolStripMenuItem;
            if (mi != null)
            {
                mi.DropDown.BackColor = Theme.Panel; mi.DropDown.ForeColor = Theme.Fg;
                foreach (ToolStripItem sub in mi.DropDownItems) ThemeItem(sub);
            }
        }

        private ContextMenuStrip BuildTreeContextMenu()
        {
            var cm = new ContextMenuStrip();
            cm.BackColor = Theme.Panel; cm.ForeColor = Theme.Fg;
            cm.Items.Add(new ToolStripMenuItem("Open", null, delegate { OpenNode(tree.SelectedNode); }));
            cm.Items.Add(new ToolStripMenuItem("New File", null, delegate { DoNewFile(); }));
            cm.Items.Add(new ToolStripMenuItem("New Folder", null, delegate { DoNewFolder(); }));
            cm.Items.Add(new ToolStripMenuItem("Rename", null, delegate { DoRename(); }));
            cm.Items.Add(new ToolStripMenuItem("Delete", null, delegate { DoDelete(); }));
            foreach (ToolStripItem it in cm.Items) ThemeItem(it);
            cm.Opening += delegate { if (tree.SelectedNode == null && tree.Nodes.Count > 0) tree.SelectedNode = tree.Nodes[0]; };
            return cm;
        }

        private Control BuildChatPanel()
        {
            var panel = new Panel(); panel.Dock = DockStyle.Fill; panel.BackColor = Theme.Bg;

            var bannerBar = new Panel();
            bannerBar.Dock = DockStyle.Top; bannerBar.Height = 40; bannerBar.BackColor = Theme.Panel;

            ollamaDot = new Label();
            ollamaDot.Dock = DockStyle.Left; ollamaDot.Width = 22; ollamaDot.TextAlign = ContentAlignment.MiddleCenter;
            ollamaDot.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            ollamaDot.ForeColor = Theme.FgDim; ollamaDot.Text = "●"; // dot
            new ToolTip().SetToolTip(ollamaDot, "Ollama status");

            icmBanner = new Label();
            icmBanner.Dock = DockStyle.Fill; icmBanner.TextAlign = ContentAlignment.MiddleLeft;
            icmBanner.Padding = new Padding(4, 0, 6, 0);
            icmBanner.ForeColor = Theme.Fg; icmBanner.Text = "No ICM loaded.";

            bannerBar.Controls.Add(icmBanner);
            bannerBar.Controls.Add(ollamaDot);

            chatLog = new RichTextBox();
            chatLog.Dock = DockStyle.Fill; chatLog.ReadOnly = true; chatLog.BorderStyle = BorderStyle.None;
            chatLog.BackColor = Theme.Bg; chatLog.ForeColor = Theme.Fg;
            chatLog.Font = new Font("Consolas", 9.75f); chatLog.WordWrap = true;

            var inputBar = new Panel(); inputBar.Dock = DockStyle.Bottom; inputBar.Height = 70; inputBar.BackColor = Theme.Panel;
            inputBar.Padding = new Padding(6);

            chatSend = new Button();
            chatSend.Text = "Send"; chatSend.Dock = DockStyle.Right; chatSend.Width = 80;
            chatSend.FlatStyle = FlatStyle.Flat; chatSend.BackColor = Theme.Panel2; chatSend.ForeColor = Theme.Fg;
            chatSend.FlatAppearance.BorderColor = Theme.Accent;
            chatSend.Click += delegate { OnSendOrCancel(); };

            chatInput = new TextBox();
            chatInput.Dock = DockStyle.Fill; chatInput.Multiline = true; chatInput.BorderStyle = BorderStyle.FixedSingle;
            chatInput.BackColor = Theme.Panel2; chatInput.ForeColor = Theme.Fg;
            chatInput.Font = new Font("Segoe UI", 10f);
            chatInput.KeyDown += delegate(object s, KeyEventArgs e)
            {
                // Enter or Ctrl+Enter sends; Shift+Enter inserts a newline.
                if (e.KeyCode == Keys.Enter && (!e.Shift || e.Control)) { e.SuppressKeyPress = true; if (!turnRunning) SendChat(); }
            };

            inputBar.Controls.Add(chatInput);
            inputBar.Controls.Add(chatSend);

            panel.Controls.Add(chatLog);
            panel.Controls.Add(inputBar);
            panel.Controls.Add(bannerBar);

            thinkTimer = new System.Windows.Forms.Timer(); thinkTimer.Interval = 400; thinkTimer.Tick += delegate { OnThinkTick(); };

            SetChatEnabled(false);
            return panel;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Now that the form has its real size, lay out the splitters: distance first (validated
            // against the live width), then the panel min-sizes consistent with that distance.
            try
            {
                outer.SplitterDistance = 190;        // small file tree
                outer.Panel1MinSize = 120;
                outer.Panel2MinSize = 360;
                outer.FixedPanel = FixedPanel.Panel1;
            }
            catch { }
            try
            {
                int d = Math.Max(220, inner.Width - 410); // editor wide, chat ~410 on the right
                if (d < inner.Width) inner.SplitterDistance = d;
                inner.Panel1MinSize = 200;
                inner.Panel2MinSize = 280;
                inner.FixedPanel = FixedPanel.Panel2;
            }
            catch { }
            Native.UseExplorerTheme(tree);
        }

        // Alt+Z is awkward as a menu ShortcutKeys value, so handle it here.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.Z)) { ToggleWordWrap(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ToggleWordWrap()
        {
            editor.WordWrap = !editor.WordWrap;
            if (wrapItem != null) wrapItem.Checked = editor.WordWrap;
            gutter.Invalidate();
            SetStatus("Word wrap " + (editor.WordWrap ? "on" : "off"));
        }

        private void GoToLine()
        {
            if (currentFile == null) { SetStatus("No file open."); return; }
            string s = Prompt("Go to line:", "1");
            int n;
            if (string.IsNullOrEmpty(s) || !int.TryParse(s, out n)) return;
            int total = editor.Lines.Length;
            if (n < 1) n = 1; if (n > total) n = Math.Max(1, total);
            int idx = editor.GetFirstCharIndexFromLine(n - 1);
            if (idx < 0) idx = editor.TextLength;
            editor.SelectionStart = idx; editor.SelectionLength = 0;
            editor.ScrollToCaret(); editor.Focus();
        }

        private void Zoom(float delta)
        {
            try
            {
                float z = editor.ZoomFactor + delta;
                if (z < 0.5f) z = 0.5f; if (z > 5f) z = 5f;
                editor.ZoomFactor = z; gutter.Invalidate();
            }
            catch { }
        }

        private void ZoomReset() { try { editor.ZoomFactor = 1f; gutter.Invalidate(); } catch { } }

        private void CloseFile()
        {
            if (currentFile == null) return;
            if (!ConfirmDiscardIfDirty()) return;
            currentFile = null; dirty = false;
            loading = true; editor.Clear(); loading = false;
            gutter.Invalidate(); UpdateTitle(); UpdateCaret(); SetStatus("Closed file.");
        }

        private void FocusTree()
        {
            if (tree.SelectedNode == null && tree.Nodes.Count > 0) tree.SelectedNode = tree.Nodes[0];
            tree.Focus();
        }

        private void FocusChat()
        {
            if (chatInput.Enabled) chatInput.Focus();
            else SetStatus("Chat is disabled (open an ICM folder).");
        }

        private void QuickOpen()
        {
            if (root == null) { SetStatus("Open a folder first."); return; }
            List<string> files = AllFiles();
            if (files.Count == 0) { SetStatus("No files in workspace."); return; }
            using (var dlg = new QuickOpenDialog(root, files))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Selected != null)
                {
                    SelectPath(dlg.Selected);
                    OpenNode(tree.SelectedNode);
                }
            }
        }

        // Flat list of every file under the workspace root (bounded, error-tolerant).
        private List<string> AllFiles()
        {
            var outl = new List<string>();
            var stack = new Stack<string>(); stack.Push(root);
            while (stack.Count > 0 && outl.Count < 20000)
            {
                string d = stack.Pop();
                try
                {
                    foreach (string f in Directory.GetFiles(d)) outl.Add(f);
                    foreach (string sub in Directory.GetDirectories(d)) stack.Push(sub);
                }
                catch { }
            }
            return outl;
        }

        // ----- workspace / tree -----

        private void DoOpenFolder()
        {
            // Modern Vista-style picker; fall back to the classic dialog if COM fails.
            string picked;
            try { picked = Native.PickFolder(Handle, root); }
            catch
            {
                var dlg = new FolderBrowserDialog();
                dlg.Description = "Choose a workspace folder (an ICM instance enables chat)";
                if (!string.IsNullOrEmpty(root)) dlg.SelectedPath = root;
                picked = (dlg.ShowDialog(this) == DialogResult.OK) ? dlg.SelectedPath : null;
            }
            if (!string.IsNullOrEmpty(picked) && Directory.Exists(picked))
                OpenWorkspace(Path.GetFullPath(picked));
        }

        private void OpenWorkspace(string path)
        {
            if (!ConfirmDiscardIfDirty()) return;
            root = path;
            currentFile = null; dirty = false; loading = true; editor.Clear(); loading = false;
            gutter.Invalidate();
            RefreshTree();
            TryLoadIcm();
            SetStatus("Workspace: " + root);
            UpdateTitle();
        }

        private void RefreshTree()
        {
            if (root == null) return;
            tree.BeginUpdate();
            tree.Nodes.Clear();
            var rootNode = new TreeNode(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)));
            rootNode.Tag = root;
            SetNodeIcon(rootNode, true, root);
            tree.Nodes.Add(rootNode);
            PopulateDir(rootNode, root);
            rootNode.Expand();
            tree.EndUpdate();
        }

        private void PopulateDir(TreeNode node, string dir)
        {
            node.Nodes.Clear();
            try
            {
                string[] dirs = Directory.GetDirectories(dir);
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                foreach (string d in dirs)
                {
                    var n = new TreeNode(Path.GetFileName(d)); n.Tag = d;
                    SetNodeIcon(n, true, d);
                    if (DirHasChildren(d)) n.Nodes.Add(new TreeNode());
                    node.Nodes.Add(n);
                }
                string[] files = Directory.GetFiles(dir);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string f in files)
                {
                    var n = new TreeNode(Path.GetFileName(f)); n.Tag = f;
                    SetNodeIcon(n, false, f);
                    node.Nodes.Add(n);
                }
            }
            catch (Exception e) { SetStatus("cannot read " + dir + ": " + e.Message); }
        }

        private void SetNodeIcon(TreeNode n, bool isDir, string path)
        {
            int idx = isDir ? icons.Folder() : icons.File(path);
            if (idx >= 0) { n.ImageIndex = idx; n.SelectedImageIndex = idx; }
        }

        private static bool DirHasChildren(string dir)
        {
            try { return Directory.GetFileSystemEntries(dir).Length > 0; }
            catch { return false; }
        }

        private void OnBeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            string path = e.Node.Tag as string;
            if (path == null || !Directory.Exists(path)) return;
            if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Tag == null) PopulateDir(e.Node, path);
        }

        // Open a file by absolute path into the editor (used when chat writes a file via "> path").
        private void OpenFilePath(string full)
        {
            if (full == null || !File.Exists(full)) return;
            if (!ConfirmDiscardIfDirty()) return;
            try
            {
                loading = true; editor.Text = File.ReadAllText(full); loading = false;
                editor.SelectionStart = 0; editor.SelectionLength = 0;
                currentFile = full; dirty = false;
                gutter.Invalidate(); UpdateTitle(); UpdateCaret();
                SetStatus("Opened " + full, Theme.Good);
            }
            catch (Exception e) { loading = false; SetStatus("Open failed: " + e.Message, Theme.Bad); }
        }

        private void OpenNode(TreeNode node)
        {
            if (node == null) return;
            string path = node.Tag as string;
            if (path == null) return;
            if (Directory.Exists(path)) { node.Toggle(); return; }
            if (!File.Exists(path)) return;
            if (!ConfirmDiscardIfDirty()) return;
            try
            {
                loading = true; editor.Text = File.ReadAllText(path); loading = false;
                editor.SelectionStart = 0; editor.SelectionLength = 0;
                currentFile = path; dirty = false;
                gutter.Invalidate(); UpdateTitle(); UpdateCaret();
                SetStatus("Opened " + path);
            }
            catch (Exception e) { loading = false; MessageBox.Show(this, e.Message, "Open failed"); }
        }

        // ----- file operations (confined to root) -----

        private string SelectedDir()
        {
            TreeNode n = tree.SelectedNode;
            if (n == null) return root;
            string p = n.Tag as string;
            if (p == null) return root;
            if (Directory.Exists(p)) return p;
            return Path.GetDirectoryName(p);
        }

        private bool InRoot(string path)
        {
            if (root == null) return false;
            string full = Path.GetFullPath(path);
            string r = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string f = full.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return f.StartsWith(r, StringComparison.OrdinalIgnoreCase);
        }

        private void DoNewFile()
        {
            if (root == null) return;
            string name = Prompt("New file name:", "newfile.txt");
            if (string.IsNullOrEmpty(name)) return;
            string path = Path.Combine(SelectedDir(), name);
            if (!InRoot(path)) { MessageBox.Show(this, "Path escapes the workspace.", "Blocked"); return; }
            try
            {
                if (File.Exists(path)) { MessageBox.Show(this, "Already exists.", "New file"); return; }
                File.WriteAllText(path, "");
                RefreshTree(); SelectPath(path); OpenNode(tree.SelectedNode);
            }
            catch (Exception e) { MessageBox.Show(this, e.Message, "New file failed"); }
        }

        private void DoNewFolder()
        {
            if (root == null) return;
            string name = Prompt("New folder name:", "newfolder");
            if (string.IsNullOrEmpty(name)) return;
            string path = Path.Combine(SelectedDir(), name);
            if (!InRoot(path)) { MessageBox.Show(this, "Path escapes the workspace.", "Blocked"); return; }
            try { Directory.CreateDirectory(path); RefreshTree(); SelectPath(path); }
            catch (Exception e) { MessageBox.Show(this, e.Message, "New folder failed"); }
        }

        private void DoRename()
        {
            TreeNode n = tree.SelectedNode;
            string p = n != null ? n.Tag as string : null;
            if (p == null || p == root) return;
            string name = Prompt("Rename to:", Path.GetFileName(p));
            if (string.IsNullOrEmpty(name)) return;
            string dest = Path.Combine(Path.GetDirectoryName(p), name);
            if (!InRoot(dest)) { MessageBox.Show(this, "Path escapes the workspace.", "Blocked"); return; }
            try
            {
                if (Directory.Exists(p)) Directory.Move(p, dest);
                else { File.Move(p, dest); if (currentFile == p) currentFile = dest; }
                RefreshTree(); SelectPath(dest); UpdateTitle();
            }
            catch (Exception e) { MessageBox.Show(this, e.Message, "Rename failed"); }
        }

        private void DoDelete()
        {
            TreeNode n = tree.SelectedNode;
            string p = n != null ? n.Tag as string : null;
            if (p == null || p == root || !InRoot(p)) return;
            bool isDir = Directory.Exists(p);
            string msg = "Delete " + (isDir ? "folder" : "file") + " '" + Path.GetFileName(p) + "'"
                       + (isDir ? " and everything in it?" : "?");
            if (MessageBox.Show(this, msg, "Delete", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try
            {
                if (isDir) Directory.Delete(p, true); else File.Delete(p);
                if (currentFile == p) { currentFile = null; loading = true; editor.Clear(); loading = false; dirty = false; gutter.Invalidate(); }
                RefreshTree(); UpdateTitle();
            }
            catch (Exception e) { MessageBox.Show(this, e.Message, "Delete failed"); }
        }

        private void DoSave()
        {
            if (currentFile == null) { SetStatus("Nothing to save."); return; }
            try { File.WriteAllText(currentFile, editor.Text); dirty = false; UpdateTitle(); SetStatus("Saved " + currentFile, Theme.Good); }
            catch (Exception e) { MessageBox.Show(this, e.Message, "Save failed"); }
        }

        private void SelectPath(string path)
        {
            if (root == null || tree.Nodes.Count == 0) return;
            string rel = path.Length > root.Length ? path.Substring(root.Length).Trim(Path.DirectorySeparatorChar) : "";
            TreeNode node = tree.Nodes[0]; node.Expand();
            if (rel.Length > 0)
            {
                foreach (string part in rel.Split(Path.DirectorySeparatorChar))
                {
                    TreeNode next = null;
                    foreach (TreeNode c in node.Nodes) { if (string.Equals(c.Text, part, StringComparison.OrdinalIgnoreCase)) { next = c; break; } }
                    if (next == null) break;
                    next.Expand(); node = next;
                }
            }
            tree.SelectedNode = node;
        }

        // ----- ICM / chat -----

        private void TryLoadIcm()
        {
            dispatcher = null;
            ollamaDot.ForeColor = Theme.FgDim;
            if (root == null || !File.Exists(Path.Combine(root, "icm.config.json")))
            {
                icmBanner.Text = "Not an ICM folder (no icm.config.json) - chat disabled.";
                SetChatEnabled(false);
                return;
            }
            try
            {
                Instance inst = Instance.Open(root);
                string env = Environment.GetEnvironmentVariable("OLLAMA_URL");
                ollamaUrl = string.IsNullOrEmpty(env) ? inst.Config.OllamaUrl : env;
                dispatcher = new Dispatcher(inst, ollamaUrl, AppendStatusFromWorker);
                icmBanner.Text = "ICM '" + inst.Config.Name + "'   dispatch=" + inst.Config.DispatchModel()
                               + "  generate=" + inst.Config.Models.Generate + "  @ " + ollamaUrl;
                ClearChatLog();
                AppendChat("icm", dispatcher.Help());
                SetChatEnabled(true);
                CheckOllamaAsync();
            }
            catch (IcmError e) { icmBanner.Text = "ICM load failed: " + e.Message; SetChatEnabled(false); }
        }

        private void CheckOllamaAsync()
        {
            string url = ollamaUrl;
            var w = new Thread(delegate()
            {
                bool up; int n = 0;
                try { List<string> tags = Ollama.Tags(url); up = true; n = tags.Count; }
                catch { up = false; }
                UiInvoke(delegate
                {
                    ollamaDot.ForeColor = up ? Theme.Good : Theme.Bad;
                    new ToolTip().SetToolTip(ollamaDot, up ? ("Ollama up (" + n + " models)") : "Ollama unreachable");
                });
            });
            w.IsBackground = true; w.Start();
        }

        private void OnSendOrCancel()
        {
            if (turnRunning) { turnCancelled = true; if (dispatcher != null) dispatcher.CancelCurrent(); SetStatus("cancelling..."); }
            else SendChat();
        }

        private void SendChat()
        {
            if (turnRunning || dispatcher == null) return;
            string line = chatInput.Text.Trim();
            if (line.Length == 0) return;
            chatInput.Clear();
            AppendChat("you", line);
            turnRunning = true; turnCancelled = false;
            SetChatEnabled(false);
            chatSend.Enabled = true; chatSend.Text = "Cancel";
            thinkTick = 0; thinkTimer.Start();

            var worker = new Thread(delegate()
            {
                TurnResult r;
                try { r = dispatcher.Turn(line); }
                catch (Exception ex) { r = new TurnResult(); r.IsError = true; r.Text = "[error] " + ex.Message; }
                UiInvoke(delegate
                {
                    thinkTimer.Stop();
                    if (turnCancelled) AppendChat("icm", "(cancelled)");
                    else if (r.Intent == "clear") { ClearChatLog(); }
                    else if (r.Intent == "quit") { ClearChatLog(); AppendChat("icm", dispatcher.Help()); }
                    else
                    {
                        AppendChat("icm", r.Text);
                        if (r.ProposedRow != null) OfferInsertRow(r.ProposedTable, r.ProposedRow);
                        if (r.WrittenPath != null) { RefreshTree(); OpenFilePath(r.WrittenPath); }
                    }
                    turnRunning = false; chatSend.Text = "Send";
                    SetChatEnabled(true);
                    SetStatus(turnCancelled ? "cancelled." : "ready.");
                    chatInput.Focus();
                });
            });
            worker.IsBackground = true; worker.Start();
        }

        private void OnThinkTick()
        {
            thinkTick++;
            SetStatus("thinking" + new string('.', 1 + (thinkTick % 3)));
        }

        // After a successful `propose`, offer to drop the validated row into samples/<table>.txt.
        private void OfferInsertRow(string table, string row)
        {
            if (root == null) return;
            DialogResult dr = MessageBox.Show(this,
                "Insert the validated row into samples\\" + table + ".txt?",
                "Propose row", MessageBoxButtons.YesNo);
            if (dr != DialogResult.Yes) return;
            string full = Path.Combine(root, "samples", table + ".txt");
            try
            {
                // make sure that table file is the one open in the editor
                if (currentFile == null || !string.Equals(currentFile, full, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(full)) { SelectPath(full); OpenNode(tree.SelectedNode); }
                    else { MessageBox.Show(this, "samples\\" + table + ".txt does not exist.", "Insert failed"); return; }
                }
                if (editor.TextLength > 0 && !editor.Text.EndsWith("\n")) editor.AppendText("\n");
                editor.AppendText(row + "\n");
                editor.SelectionStart = editor.TextLength; editor.ScrollToCaret();
                dirty = true; UpdateTitle();
                SetStatus("Row inserted into " + table + " - review and Ctrl+S to save.", Theme.Good);
            }
            catch (Exception e) { MessageBox.Show(this, e.Message, "Insert failed"); }
        }

        private void ValidateCurrentFile()
        {
            if (dispatcher == null || dispatcher.Icm == null) { AppendChat("icm", "Open an ICM folder to validate."); return; }
            if (currentFile == null) { SetStatus("No file open to validate."); return; }
            string table = Path.GetFileNameWithoutExtension(currentFile);
            string schemaPath;
            try { schemaPath = dispatcher.Icm.Resolve("schemas/" + table + ".json"); }
            catch (IcmError e) { AppendChat("icm", "[error] " + e.Message); return; }
            if (!File.Exists(schemaPath)) { AppendChat("icm", "No schema schemas/" + table + ".json for this file."); return; }
            try
            {
                TableSchema schema = TableSchema.Load(schemaPath);
                List<Problem> problems = Oracle.ValidateTsv(schema, editor.Text, null);
                if (problems.Count == 0) { AppendChat("icm", "PASS - '" + table + "' (editor buffer) is valid under its schema."); return; }
                var sb = new System.Text.StringBuilder();
                sb.Append("FAIL - " + problems.Count + " problem(s) in '" + table + "' (editor buffer):\n");
                int shown = 0;
                foreach (Problem p in problems) { if (shown++ >= 40) break; sb.Append("  " + p.ToString() + "\n"); }
                AppendChat("icm", sb.ToString());
            }
            catch (IcmError e) { AppendChat("icm", "[error] " + e.Message); }
        }

        private void AppendStatusFromWorker(string msg)
        {
            UiInvoke(delegate { AppendLogLine("  - " + msg, Theme.FgDim); });
        }

        private void AppendChat(string who, string text)
        {
            if (who == "you")
            {
                AppendLabel("you", Color.LightSkyBlue);
                AppendLogLine(text, Theme.Fg);
                AppendLogLine("", Theme.Fg);
                return;
            }
            if (who == "icm")
            {
                // Oracle status lines keep their pass/fail colour and stay plain; everything else is
                // the model's answer, rendered as markdown.
                if (text.StartsWith("PASS"))
                {
                    AppendLabel("icm", Theme.FgDim); AppendLogLine(text, Theme.Good); AppendLogLine("", Theme.Fg); return;
                }
                if (text.StartsWith("FAIL") || text.StartsWith("[error]") || text.StartsWith("dispatch failed"))
                {
                    AppendLabel("icm", Theme.FgDim); AppendLogLine(text, Theme.Bad); AppendLogLine("", Theme.Fg); return;
                }
                AppendLabel("icm", Theme.FgDim);
                ChatMarkdown.Render(chatLog, text);
                AppendLogLine("", Theme.Fg);
                return;
            }
            AppendLogLine(who + ": " + text, Theme.Fg);
            AppendLogLine("", Theme.Fg);
        }

        // A bold role label on its own line (chat header above the message body).
        private void AppendLabel(string who, Color color)
        {
            chatLog.SelectionStart = chatLog.TextLength; chatLog.SelectionLength = 0;
            chatLog.SelectionFont = ChatMarkdown.GetFont("Segoe UI", 9f, FontStyle.Bold);
            chatLog.SelectionColor = color;
            chatLog.SelectionBackColor = Theme.Bg;
            chatLog.AppendText(who + "\n");
        }

        private void AppendLogLine(string text, Color color)
        {
            chatLog.SelectionStart = chatLog.TextLength; chatLog.SelectionLength = 0;
            chatLog.SelectionFont = ChatMarkdown.GetFont("Consolas", 9.75f, FontStyle.Regular);
            chatLog.SelectionColor = color;
            chatLog.SelectionBackColor = Theme.Bg;   // clear any residual code-block highlight
            chatLog.AppendText(text + "\n");
            chatLog.SelectionColor = chatLog.ForeColor;
            chatLog.SelectionStart = chatLog.TextLength;
            chatLog.ScrollToCaret();
        }

        private void ClearChatLog() { chatLog.Clear(); }

        private void SetChatEnabled(bool on)
        {
            bool ready = on && dispatcher != null && !turnRunning;
            chatInput.Enabled = ready;
            chatSend.Enabled = ready || turnRunning; // stays enabled during a turn to allow Cancel
        }

        // ----- helpers -----

        private void ShowFind(bool replace)
        {
            if (findDialog == null || findDialog.IsDisposed) findDialog = new FindDialog(editor);
            findDialog.SetReplaceVisible(replace);
            if (!findDialog.Visible) findDialog.Show(this);
            findDialog.Activate();
            findDialog.FocusFind(editor.SelectedText);
        }

        private void UiInvoke(MethodInvoker action)
        {
            if (IsDisposed) return;
            try { if (InvokeRequired) BeginInvoke(action); else action(); } catch (ObjectDisposedException) { }
        }

        private bool ConfirmDiscardIfDirty()
        {
            if (!dirty || currentFile == null) return true;
            DialogResult r = MessageBox.Show(this, "Save changes to " + Path.GetFileName(currentFile) + "?",
                "Unsaved changes", MessageBoxButtons.YesNoCancel);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes) DoSave();
            return true;
        }

        private void UpdateTitle()
        {
            string f = currentFile != null ? Path.GetFileName(currentFile) : "(no file)";
            Text = "icm-gui - " + (dirty ? "● " : "") + f + (root != null ? "   [" + root + "]" : "");
        }

        private void UpdateCaret()
        {
            if (currentFile == null) { caretLabel.Text = ""; return; }
            int idx = editor.SelectionStart;
            int line = editor.GetLineFromCharIndex(idx);
            int col = idx - editor.GetFirstCharIndexFromLine(line);
            caretLabel.Text = "Ln " + (line + 1) + ", Col " + (col + 1);
        }

        private void SetStatus(string s) { SetStatus(s, Theme.Fg); }
        private void SetStatus(string s, Color c) { statusLabel.ForeColor = c; statusLabel.Text = s; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!ConfirmDiscardIfDirty()) e.Cancel = true;
            base.OnFormClosing(e);
        }

        // A tiny modal text prompt (WinForms has no built-in InputBox), themed to match.
        private string Prompt(string label, string initial)
        {
            using (var dlg = new Form())
            {
                dlg.Text = label; dlg.Width = 400; dlg.Height = 150;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false; dlg.MaximizeBox = false;
                dlg.BackColor = Theme.Bg; dlg.ForeColor = Theme.Fg;
                var lbl = new Label() { Left = 12, Top = 12, Width = 360, Text = label, ForeColor = Theme.Fg };
                var tb = new TextBox() { Left = 12, Top = 38, Width = 360, Text = initial, BackColor = Theme.Panel2, ForeColor = Theme.Fg, BorderStyle = BorderStyle.FixedSingle };
                var ok = new Button() { Text = "OK", Left = 214, Top = 72, Width = 75, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel2, ForeColor = Theme.Fg };
                var cancel = new Button() { Text = "Cancel", Left = 297, Top = 72, Width = 75, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel2, ForeColor = Theme.Fg };
                dlg.Controls.Add(lbl); dlg.Controls.Add(tb); dlg.Controls.Add(ok); dlg.Controls.Add(cancel);
                dlg.AcceptButton = ok; dlg.CancelButton = cancel;
                tb.SelectAll();
                return dlg.ShowDialog(this) == DialogResult.OK ? tb.Text.Trim() : null;
            }
        }
    }

    // A modeless find/replace dialog operating on a RichTextBox.
    internal class FindDialog : Form
    {
        private readonly RichTextBox target;
        private readonly TextBox findBox = new TextBox();
        private readonly TextBox replaceBox = new TextBox();
        private readonly Label replaceLabel = new Label();
        private readonly Button replaceBtn = new Button();
        private readonly Button replaceAllBtn = new Button();

        public FindDialog(RichTextBox target)
        {
            this.target = target;
            Text = "Find / Replace";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            Width = 420; Height = 130; ShowInTaskbar = false;
            BackColor = Theme.Bg; ForeColor = Theme.Fg;

            var lf = new Label() { Left = 10, Top = 14, Width = 60, Text = "Find:", ForeColor = Theme.Fg };
            findBox.Left = 74; findBox.Top = 11; findBox.Width = 230; findBox.BackColor = Theme.Panel2; findBox.ForeColor = Theme.Fg; findBox.BorderStyle = BorderStyle.FixedSingle;
            var findBtn = new Button() { Text = "Find Next", Left = 312, Top = 9, Width = 90, FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel2, ForeColor = Theme.Fg };
            findBtn.Click += delegate { FindNext(); };

            replaceLabel.Left = 10; replaceLabel.Top = 46; replaceLabel.Width = 60; replaceLabel.Text = "Replace:"; replaceLabel.ForeColor = Theme.Fg;
            replaceBox.Left = 74; replaceBox.Top = 43; replaceBox.Width = 230; replaceBox.BackColor = Theme.Panel2; replaceBox.ForeColor = Theme.Fg; replaceBox.BorderStyle = BorderStyle.FixedSingle;
            replaceBtn.Text = "Replace"; replaceBtn.Left = 312; replaceBtn.Top = 41; replaceBtn.Width = 90; replaceBtn.FlatStyle = FlatStyle.Flat; replaceBtn.BackColor = Theme.Panel2; replaceBtn.ForeColor = Theme.Fg;
            replaceBtn.Click += delegate { ReplaceOne(); };
            replaceAllBtn.Text = "Replace All"; replaceAllBtn.Left = 312; replaceAllBtn.Top = 71; replaceAllBtn.Width = 90; replaceAllBtn.FlatStyle = FlatStyle.Flat; replaceAllBtn.BackColor = Theme.Panel2; replaceAllBtn.ForeColor = Theme.Fg;
            replaceAllBtn.Click += delegate { ReplaceAll(); };

            Controls.Add(lf); Controls.Add(findBox); Controls.Add(findBtn);
            Controls.Add(replaceLabel); Controls.Add(replaceBox); Controls.Add(replaceBtn); Controls.Add(replaceAllBtn);
            AcceptButton = findBtn;
            findBox.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; FindNext(); } };
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Hide(); } };
        }

        public void SetReplaceVisible(bool on)
        {
            replaceLabel.Visible = replaceBox.Visible = replaceBtn.Visible = replaceAllBtn.Visible = on;
            Height = on ? 130 : 80;
        }

        public void FocusFind(string seed)
        {
            if (!string.IsNullOrEmpty(seed) && seed.IndexOf('\n') < 0) findBox.Text = seed;
            findBox.Focus(); findBox.SelectAll();
        }

        private void FindNext()
        {
            string term = findBox.Text;
            if (term.Length == 0) return;
            int start = target.SelectionStart + target.SelectionLength;
            int idx = target.Find(term, start, RichTextBoxFinds.None);
            if (idx < 0 && start > 0) idx = target.Find(term, 0, start, RichTextBoxFinds.None); // wrap
            if (idx >= 0) { target.Select(idx, term.Length); target.ScrollToCaret(); }
            else SystemSounds_Beep();
        }

        private void ReplaceOne()
        {
            string term = findBox.Text;
            if (term.Length == 0) return;
            if (target.SelectionLength == term.Length && string.Equals(target.SelectedText, term, StringComparison.Ordinal))
                target.SelectedText = replaceBox.Text;
            FindNext();
        }

        private void ReplaceAll()
        {
            string term = findBox.Text;
            if (term.Length == 0) return;
            int count = 0, from = 0;
            while (true)
            {
                int idx = target.Find(term, from, RichTextBoxFinds.None);
                if (idx < 0) break;
                target.Select(idx, term.Length);
                target.SelectedText = replaceBox.Text;
                from = idx + replaceBox.Text.Length;
                count++;
                if (count > 100000) break;
            }
            MessageBox.Show(this, "Replaced " + count + " occurrence(s).", "Replace All");
        }

        private static void SystemSounds_Beep() { try { System.Media.SystemSounds.Beep.Play(); } catch { } }
    }

    // Ctrl+P quick-open: type to fuzzy-filter files by relative path; Up/Down + Enter to open.
    internal class QuickOpenDialog : Form
    {
        private readonly string rootSep;
        private readonly List<string> all; // full paths
        private readonly TextBox box = new TextBox();
        private readonly ListBox list = new ListBox();
        public string Selected;

        public QuickOpenDialog(string root, List<string> allFiles)
        {
            rootSep = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            all = allFiles;
            Text = "Quick Open";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            Width = 580; Height = 440; ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
            BackColor = Theme.Bg; ForeColor = Theme.Fg;

            box.Dock = DockStyle.Top; box.BackColor = Theme.Panel2; box.ForeColor = Theme.Fg;
            box.BorderStyle = BorderStyle.FixedSingle; box.Font = new Font("Segoe UI", 11f);
            list.Dock = DockStyle.Fill; list.BackColor = Theme.Panel; list.ForeColor = Theme.Fg;
            list.BorderStyle = BorderStyle.None; list.Font = new Font("Consolas", 9.5f); list.IntegralHeight = false;

            Controls.Add(list);
            Controls.Add(box);

            box.TextChanged += delegate { Refilter(); };
            box.KeyDown += BoxKey;
            list.DoubleClick += delegate { Choose(); };
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };

            Refilter();
            box.Select();
        }

        private string Rel(string full) { return full.Length > rootSep.Length ? full.Substring(rootSep.Length) : full; }

        private void Refilter()
        {
            string q = box.Text.Trim();
            list.BeginUpdate();
            list.Items.Clear();
            var matches = new List<string>();
            foreach (string f in all)
            {
                string rel = Rel(f);
                if (q.Length == 0 || Subsequence(rel, q)) matches.Add(rel);
                if (matches.Count >= 500) break;
            }
            matches.Sort(delegate(string a, string b) { return a.Length.CompareTo(b.Length); });
            foreach (string m in matches) list.Items.Add(m);
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            list.EndUpdate();
        }

        // Case-insensitive subsequence match (VSCode-ish fuzzy).
        private static bool Subsequence(string text, string q)
        {
            int ti = 0;
            foreach (char c in q)
            {
                char lc = char.ToLowerInvariant(c);
                bool found = false;
                while (ti < text.Length) { if (char.ToLowerInvariant(text[ti++]) == lc) { found = true; break; } }
                if (!found) return false;
            }
            return true;
        }

        private void BoxKey(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { if (list.Items.Count > 0) list.SelectedIndex = Math.Min(list.Items.Count - 1, list.SelectedIndex + 1); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Up) { if (list.Items.Count > 0) list.SelectedIndex = Math.Max(0, list.SelectedIndex - 1); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { Choose(); e.SuppressKeyPress = true; }
        }

        private void Choose()
        {
            if (list.SelectedItem == null) return;
            Selected = Path.Combine(rootSep, list.SelectedItem.ToString());
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

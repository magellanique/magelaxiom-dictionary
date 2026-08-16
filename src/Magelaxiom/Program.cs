using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Magelaxiom
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DictionaryForm());
        }
    }

    internal sealed class DictionaryForm : Form
    {
        private static readonly Color Black = Color.FromArgb(46, 1, 56);
        private static readonly Color NearBlack = Color.FromArgb(36, 0, 44);
        private static readonly Color PanelBlack = Color.FromArgb(41, 1, 50);
        private static readonly Color Border = Color.FromArgb(86, 38, 98);
        private static readonly Color TextMain = Color.FromArgb(255, 255, 255);
        private static readonly Color TextMuted = Color.FromArgb(215, 190, 220);
        private static readonly Color Accent = Color.FromArgb(246, 231, 248);

        private readonly Image logoImage;
        private readonly TextBox searchBox;
        private readonly Button saveButton;
        private readonly ResultView resultView;
        private readonly Label statusLabel;
        private readonly ListBox savedList;
        private readonly Timer searchTimer;
        private readonly string savedPath;
        private int searchVersion;
        private LookupResult currentResult;

        public DictionaryForm()
        {
            Text = "Magelaxiom";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            TopMost = true;
            ClientSize = new Size(640, 640);
            BackColor = Black;
            Font = new Font("Segoe UI", 9f);
            TrySetWindowIcon();
            logoImage = LoadEmbeddedImage("Magelaxiom.logo.png");

            savedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Magelaxiom",
                "saved_words.txt");

            var logoBox = new PictureBox();
            logoBox.Image = logoImage;
            logoBox.SizeMode = PictureBoxSizeMode.Zoom;
            logoBox.BackColor = Black;
            logoBox.Location = new Point(16, 10);
            logoBox.Size = new Size(34, 34);

            var titleLabel = new Label();
            titleLabel.Text = "Magelaxiom";
            titleLabel.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
            titleLabel.ForeColor = TextMain;
            titleLabel.BackColor = Black;
            titleLabel.Location = new Point(58, 14);
            titleLabel.Size = new Size(180, 26);

            statusLabel = new Label();
            statusLabel.Text = "";
            statusLabel.ForeColor = TextMuted;
            statusLabel.BackColor = Black;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.Location = new Point(330, 17);
            statusLabel.Size = new Size(294, 22);

            searchBox = new TextBox();
            searchBox.Location = new Point(16, 52);
            searchBox.Size = new Size(512, 28);
            searchBox.BackColor = NearBlack;
            searchBox.ForeColor = TextMain;
            searchBox.BorderStyle = BorderStyle.FixedSingle;
            searchBox.Font = new Font("Segoe UI", 11f);
            searchBox.KeyDown += OnSearchBoxKeyDown;
            searchBox.TextChanged += OnSearchBoxTextChanged;

            saveButton = new Button();
            saveButton.Text = "Save";
            saveButton.Location = new Point(536, 51);
            saveButton.Size = new Size(88, 31);
            StyleButton(saveButton, false);
            saveButton.Click += delegate { SaveCurrent(); };

            resultView = new ResultView();
            resultView.Location = new Point(16, 96);
            resultView.Size = new Size(608, 404);
            resultView.BackColor = Black;
            resultView.SetMessage("Start typing a word or phrase.\n\nExamples: ceteris paribus, ex-post, the buck stops here");

            var savedHeader = new Label();
            savedHeader.Text = "Saved";
            savedHeader.ForeColor = TextMuted;
            savedHeader.BackColor = Black;
            savedHeader.Location = new Point(16, 516);
            savedHeader.Size = new Size(120, 22);

            var openButton = new Button();
            openButton.Text = "Open";
            openButton.Location = new Point(444, 511);
            openButton.Size = new Size(84, 29);
            StyleButton(openButton, false);
            openButton.Click += delegate { OpenSaved(); };

            var removeButton = new Button();
            removeButton.Text = "Remove";
            removeButton.Location = new Point(536, 511);
            removeButton.Size = new Size(88, 29);
            StyleButton(removeButton, false);
            removeButton.Click += delegate { RemoveSaved(); };

            savedList = new ListBox();
            savedList.Location = new Point(16, 548);
            savedList.Size = new Size(608, 76);
            savedList.BorderStyle = BorderStyle.FixedSingle;
            savedList.BackColor = PanelBlack;
            savedList.ForeColor = TextMain;
            savedList.Font = new Font("Segoe UI", 10f);
            savedList.DoubleClick += delegate { OpenSaved(); };

            searchTimer = new Timer();
            searchTimer.Interval = 180;
            searchTimer.Tick += delegate
            {
                searchTimer.Stop();
                BeginSearch();
            };

            Controls.Add(logoBox);
            Controls.Add(titleLabel);
            Controls.Add(statusLabel);
            Controls.Add(searchBox);
            Controls.Add(saveButton);
            Controls.Add(resultView);
            Controls.Add(savedHeader);
            Controls.Add(openButton);
            Controls.Add(removeButton);
            Controls.Add(savedList);

            LoadSavedTerms();
            searchBox.Focus();
            Task.Run(delegate { OfflineDictionary.Preload(); });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (logoImage != null)
                {
                    logoImage.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.L))
            {
                searchBox.Focus();
                searchBox.SelectAll();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TryUseDarkTitleBar(Handle);
        }

        private static void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.BackColor = primary ? Color.FromArgb(66, 8, 78) : NearBlack;
            button.ForeColor = TextMain;
            button.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        }

        private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                searchTimer.Stop();
                BeginSearch();
            }
        }

        private void OnSearchBoxTextChanged(object sender, EventArgs e)
        {
            searchTimer.Stop();
            currentResult = null;

            var term = searchBox.Text.Trim();
            if (term.Length == 0)
            {
                searchVersion++;
                statusLabel.Text = "";
                SetPlainText("Start typing a word or phrase.\n\nExamples: ceteris paribus, ex-post, the buck stops here");
                return;
            }

            statusLabel.Text = "Typing...";
            searchTimer.Start();
        }

        private async void BeginSearch()
        {
            var term = searchBox.Text.Trim();
            if (term.Length == 0)
            {
                statusLabel.Text = "Enter a search term.";
                return;
            }

            var version = ++searchVersion;
            statusLabel.Text = "Searching...";
            SetPlainText("Searching for " + term + "...");

            try
            {
                var result = await Task.Run(delegate { return OfflineDictionary.Instance.Lookup(term); });
                if (version != searchVersion)
                {
                    return;
                }

                currentResult = result;
                statusLabel.Text = result.Found ? "Found." : "No exact entry found.";
                RenderResult(result);
            }
            catch (Exception ex)
            {
                if (version != searchVersion)
                {
                    return;
                }

                statusLabel.Text = "Lookup failed.";
                SetPlainText("Lookup failed:\n\n" + ex.Message);
            }
            finally
            {
                if (version == searchVersion)
                {
                    searchBox.Focus();
                }
            }
        }

        private void SetPlainText(string text)
        {
            resultView.SetMessage(text);
        }

        private void RenderResult(LookupResult result)
        {
            resultView.SetResult(result);
        }

        private void SaveCurrent()
        {
            var value = "";
            if (currentResult != null && currentResult.MatchedTerm.Length > 0)
            {
                value = currentResult.MatchedTerm;
            }
            else
            {
                value = searchBox.Text.Trim();
            }

            if (value.Length == 0)
            {
                statusLabel.Text = "Nothing to save.";
                return;
            }

            if (!ListContains(savedList, value))
            {
                savedList.Items.Add(value);
                PersistSavedTerms();
                statusLabel.Text = "Saved.";
            }
            else
            {
                statusLabel.Text = "Already saved.";
            }
        }

        private void OpenSaved()
        {
            if (savedList.SelectedItem == null)
            {
                return;
            }

            searchBox.Text = savedList.SelectedItem.ToString();
            BeginSearch();
        }

        private void RemoveSaved()
        {
            if (savedList.SelectedItem == null)
            {
                return;
            }

            var index = savedList.SelectedIndex;
            savedList.Items.RemoveAt(index);
            PersistSavedTerms();
            statusLabel.Text = "Removed.";
        }

        private static bool ListContains(ListBox list, string value)
        {
            foreach (var item in list.Items)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(item.ToString(), value))
                {
                    return true;
                }
            }

            return false;
        }

        private void LoadSavedTerms()
        {
            try
            {
                if (!File.Exists(savedPath))
                {
                    return;
                }

                foreach (var line in File.ReadAllLines(savedPath, Encoding.UTF8))
                {
                    var value = line.Trim();
                    if (value.Length > 0 && !ListContains(savedList, value))
                    {
                        savedList.Items.Add(value);
                    }
                }
            }
            catch
            {
                statusLabel.Text = "Could not load saved words.";
            }
        }

        private void PersistSavedTerms()
        {
            try
            {
                var directory = Path.GetDirectoryName(savedPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var values = new List<string>();
                foreach (var item in savedList.Items)
                {
                    values.Add(item.ToString());
                }

                File.WriteAllLines(savedPath, values.ToArray(), Encoding.UTF8);
            }
            catch
            {
                statusLabel.Text = "Could not save list.";
            }
        }

        private void TrySetWindowIcon()
        {
            try
            {
                var associatedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (associatedIcon != null)
                {
                    Icon = associatedIcon;
                }
            }
            catch
            {
            }
        }

        private static Image LoadEmbeddedImage(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Missing embedded resource: " + resourceName);
                }

                using (var image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
        }

        private static void TryUseDarkTitleBar(IntPtr handle)
        {
            try
            {
                if (Environment.OSVersion.Version.Major < 10)
                {
                    return;
                }

                var enabled = 1;
                var result = DwmSetWindowAttribute(handle, 20, ref enabled, Marshal.SizeOf(typeof(int)));
                if (result != 0)
                {
                    DwmSetWindowAttribute(handle, 19, ref enabled, Marshal.SizeOf(typeof(int)));
                }

                var captionColor = ColorTranslator.ToWin32(Black);
                DwmSetWindowAttribute(handle, 35, ref captionColor, Marshal.SizeOf(typeof(int)));

                var textColor = ColorTranslator.ToWin32(TextMain);
                DwmSetWindowAttribute(handle, 36, ref textColor, Marshal.SizeOf(typeof(int)));
            }
            catch
            {
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }

    internal sealed class ResultView : ScrollableControl
    {
        private static readonly Color Black = Color.FromArgb(46, 1, 56);
        private static readonly Color TextMain = Color.FromArgb(255, 255, 255);
        private static readonly Color TextMuted = Color.FromArgb(215, 190, 220);
        private static readonly Color Gold = Color.FromArgb(246, 231, 248);

        private readonly Font headwordFont;
        private readonly Font sectionFont;
        private readonly Font bodyFont;
        private readonly Font bodyBoldFont;
        private LookupResult result;
        private string message;

        public ResultView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            AutoScroll = true;
            TabStop = false;
            BackColor = Black;
            headwordFont = new Font("Segoe UI", 24f, FontStyle.Bold);
            sectionFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            bodyFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            bodyBoldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            message = "";
        }

        public void SetMessage(string value)
        {
            result = null;
            message = value == null ? "" : value;
            AutoScrollPosition = new Point(0, 0);
            Invalidate();
        }

        public void SetResult(LookupResult value)
        {
            result = value;
            message = "";
            AutoScrollPosition = new Point(0, 0);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                headwordFont.Dispose();
                sectionFont.Dispose();
                bodyFont.Dispose();
                bodyBoldFont.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var graphics = e.Graphics;
            graphics.Clear(Black);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

            var y = 10;
            var width = ClientSize.Width - 18;
            if (result == null)
            {
                y = DrawMessage(graphics, y, width);
            }
            else
            {
                y = DrawResult(graphics, y, width, result);
            }

            var requiredHeight = Math.Max(ClientSize.Height, y + 16);
            if (AutoScrollMinSize.Height != requiredHeight)
            {
                AutoScrollMinSize = new Size(0, requiredHeight);
            }
        }

        private int DrawMessage(Graphics graphics, int y, int width)
        {
            DrawWrapped(graphics, message, bodyFont, TextMuted, 2, y + 8, width - 10);
            return y + 90;
        }

        private int DrawResult(Graphics graphics, int y, int width, LookupResult value)
        {
            var title = value.MatchedTerm.Length > 0 ? value.MatchedTerm : value.Query;
            y += DrawWrapped(graphics, title, headwordFont, TextMain, 2, y, width - 10);
            y += 10;

            y = DrawSectionLabel(graphics, y, "English", true);
            if (value.Meanings.Count == 0)
            {
                y += DrawWrapped(graphics, "No definition found.", bodyFont, TextMuted, 6, y + 4, width - 20);
            }
            else
            {
                y = DrawNumberedList(graphics, y + 4, width, value.Meanings, 8);
            }

            if (value.Synonyms.Count > 0)
            {
                y += 10;
                y = DrawSectionLabel(graphics, y, "Synonyms", false);
                y += DrawWrapped(graphics, JoinLimited(value.Synonyms, 18), bodyBoldFont, TextMain, 6, y + 4, width - 20);
            }

            if (value.Antonyms.Count > 0)
            {
                y += 10;
                y = DrawSectionLabel(graphics, y, "Antonyms", false);
                y += DrawWrapped(graphics, JoinLimited(value.Antonyms, 18), bodyBoldFont, TextMain, 6, y + 4, width - 20);
            }

            return y;
        }

        private int DrawSectionLabel(Graphics graphics, int y, string label, bool bookIcon)
        {
            using (var pen = new Pen(Gold, 1.4f))
            {
                if (bookIcon)
                {
                    graphics.DrawRectangle(pen, 2, y + 4, 9, 8);
                    graphics.DrawLine(pen, 7, y + 4, 7, y + 12);
                }
                else
                {
                    graphics.DrawLine(pen, 4, y + 8, 12, y + 8);
                    graphics.DrawLine(pen, 8, y + 4, 8, y + 12);
                }
            }

            DrawSingleLine(graphics, label, sectionFont, Gold, 20, y + 1, ClientSize.Width - 40);
            return y + 22;
        }

        private int DrawNumberedList(Graphics graphics, int y, int width, List<string> values, int limit)
        {
            var count = Math.Min(values.Count, limit);
            for (var index = 0; index < count; index++)
            {
                var number = (index + 1).ToString() + ".";
                DrawSingleLine(graphics, number, bodyBoldFont, TextMain, 6, y, 32);
                var height = DrawWrapped(graphics, values[index], bodyBoldFont, TextMain, 38, y, width - 52);
                y += Math.Max(22, height + 5);
            }

            if (values.Count > limit)
            {
                y += DrawWrapped(graphics, "+" + (values.Count - limit).ToString() + " more", bodyFont, TextMuted, 38, y, width - 52);
            }

            return y;
        }

        private static void DrawSingleLine(Graphics graphics, string text, Font font, Color color, int x, int y, int width)
        {
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat())
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                graphics.DrawString(text, font, brush, new RectangleF(x, y, width, font.GetHeight(graphics) + 3), format);
            }
        }

        private int DrawWrapped(Graphics graphics, string text, Font font, Color color, int x, int y, int width)
        {
            var height = MeasureWrapped(graphics, text, font, width);
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat())
            {
                format.Trimming = StringTrimming.Word;
                graphics.DrawString(text, font, brush, new RectangleF(x, y, width, height + 4), format);
            }

            return height;
        }

        private static int MeasureWrapped(Graphics graphics, string text, Font font, int width)
        {
            if (text == null || text.Length == 0)
            {
                return 0;
            }

            using (var format = new StringFormat())
            {
                format.Trimming = StringTrimming.Word;
                var size = graphics.MeasureString(text, font, Math.Max(20, width), format);
                return (int)Math.Ceiling(size.Height);
            }
        }

        private static string JoinLimited(List<string> values, int limit)
        {
            var result = new List<string>();
            for (var index = 0; index < values.Count && index < limit; index++)
            {
                result.Add(values[index]);
            }

            if (values.Count > limit)
            {
                result.Add("+" + (values.Count - limit).ToString() + " more");
            }

            return string.Join(", ", result.ToArray());
        }

    }

    internal sealed class OfflineDictionary
    {
        private const string DictionaryResourceName = "Magelaxiom.dictionary.bin";

        private static readonly Lazy<OfflineDictionary> LazyInstance =
            new Lazy<OfflineDictionary>(delegate { return new OfflineDictionary(); });

        private readonly BinaryLexicon dictionaryLexicon;

        private OfflineDictionary()
        {
            dictionaryLexicon = BinaryLexicon.FromResource(DictionaryResourceName);
        }

        public static OfflineDictionary Instance
        {
            get { return LazyInstance.Value; }
        }

        public static void Preload()
        {
            var unused = Instance;
        }

        public LookupResult Lookup(string query)
        {
            var normalized = TextTools.NormalizeQuery(query);
            var candidates = TextTools.BuildCandidates(normalized);
            var matched = "";
            List<string> rows;
            TermEntry termEntry;

            if (TryFindRows(dictionaryLexicon, candidates, out matched, out rows))
            {
                termEntry = MaterializeTerm(matched, rows);
            }
            else
            {
                matched = normalized;
                termEntry = new TermEntry(matched);
            }

            var result = new LookupResult();
            result.Query = query;
            result.MatchedTerm = matched;
            result.Meanings.AddRange(termEntry.Meanings);
            result.Synonyms.AddRange(termEntry.Synonyms);
            result.Antonyms.AddRange(termEntry.Antonyms);
            result.Examples.AddRange(termEntry.Examples);
            result.Found = result.Meanings.Count > 0 ||
                result.Synonyms.Count > 0 ||
                result.Antonyms.Count > 0;
            return result;
        }

        private static bool TryFindRows(BinaryLexicon lexicon, List<string> candidates, out string matched, out List<string> rows)
        {
            foreach (var candidate in candidates)
            {
                if (lexicon.TryFind(candidate, out matched, out rows))
                {
                    return true;
                }
            }

            matched = "";
            rows = null;
            return false;
        }

        private TermEntry MaterializeTerm(string term, List<string> rows)
        {
            var entry = new TermEntry(term);
            foreach (var row in rows)
            {
                var parts = SplitTsv(row);
                if (parts.Length < 6)
                {
                    continue;
                }

                var pos = Unescape(parts[1]);
                var definition = Unescape(parts[2]);
                var synonyms = SplitList(Unescape(parts[3]), ", ");
                var antonyms = SplitList(Unescape(parts[4]), ", ");
                var examples = SplitList(Unescape(parts[5]), " | ");

                if (definition.Length > 0)
                {
                    var prefix = pos.Length > 0 ? pos + ". " : "";
                    entry.AddMeaning(prefix + definition);
                }

                entry.AddSynonyms(synonyms);
                entry.AddAntonyms(antonyms);
                entry.AddExamples(examples);
            }

            return entry;
        }

        private static string[] SplitTsv(string line)
        {
            return line.Split(new[] { '\t' });
        }

        private static List<string> SplitList(string value, string separator)
        {
            var result = new List<string>();
            if (value.Length == 0)
            {
                return result;
            }

            foreach (var item in value.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries))
            {
                var cleaned = item.Trim();
                if (cleaned.Length > 0 && !ContainsIgnoreCase(result, cleaned))
                {
                    result.Add(cleaned);
                }
            }

            return result;
        }

        private static string Unescape(string value)
        {
            return value
                .Replace("\\t", "\t")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\\\", "\\");
        }

        private static bool ContainsIgnoreCase(ICollection<string> values, string candidate)
        {
            foreach (var value in values)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(value, candidate))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class BinaryLexicon
    {
        private const int Magic = 0x58444c4f;
        private const int HeaderSize = 16;
        private const int RecordSize = 16;
        private const int LooseRecordSize = 12;

        private readonly byte[] data;
        private readonly int recordCount;
        private readonly int looseCount;
        private readonly int recordsOffset;
        private readonly int looseOffset;
        private readonly int blobOffset;

        private BinaryLexicon(byte[] data)
        {
            this.data = data;
            if (data.Length < HeaderSize || ReadInt32(0) != Magic)
            {
                throw new InvalidOperationException("Invalid dictionary index resource.");
            }

            var version = ReadInt32(4);
            if (version != 1)
            {
                throw new InvalidOperationException("Unsupported dictionary index version.");
            }

            recordCount = ReadInt32(8);
            looseCount = ReadInt32(12);
            recordsOffset = HeaderSize;
            looseOffset = recordsOffset + checked(recordCount * RecordSize);
            blobOffset = looseOffset + checked(looseCount * LooseRecordSize);
            if (blobOffset > data.Length)
            {
                throw new InvalidOperationException("Corrupt dictionary index resource.");
            }
        }

        public static BinaryLexicon FromResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Missing embedded resource: " + resourceName);
                }

                var buffer = new byte[stream.Length];
                var offset = 0;
                while (offset < buffer.Length)
                {
                    var read = stream.Read(buffer, offset, buffer.Length - offset);
                    if (read == 0)
                    {
                        break;
                    }

                    offset += read;
                }

                if (offset != buffer.Length)
                {
                    throw new EndOfStreamException("Could not read embedded resource: " + resourceName);
                }

                return new BinaryLexicon(buffer);
            }
        }

        public bool TryFind(string term, out string matched, out List<string> rows)
        {
            term = TextTools.NormalizeQuery(term);
            var index = FindRecord(term);
            if (index < 0)
            {
                index = FindLoose(TextTools.LooseKey(term));
            }

            if (index < 0)
            {
                matched = "";
                rows = null;
                return false;
            }

            matched = ReadRecordKey(index);
            rows = ReadRows(index);
            return true;
        }

        private int FindRecord(string key)
        {
            if (key.Length == 0)
            {
                return -1;
            }

            var low = 0;
            var high = recordCount - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                var midKey = ReadRecordKey(mid);
                var comparison = StringComparer.OrdinalIgnoreCase.Compare(midKey, key);
                if (comparison == 0)
                {
                    return mid;
                }

                if (comparison < 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return -1;
        }

        private int FindLoose(string key)
        {
            if (key.Length == 0)
            {
                return -1;
            }

            var low = 0;
            var high = looseCount - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                var midKey = ReadLooseKey(mid);
                var comparison = StringComparer.OrdinalIgnoreCase.Compare(midKey, key);
                if (comparison == 0)
                {
                    return ReadInt32(LooseRecordOffset(mid) + 8);
                }

                if (comparison < 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return -1;
        }

        private string ReadRecordKey(int index)
        {
            var offset = RecordOffset(index);
            return ReadBlobString(ReadInt32(offset), ReadInt32(offset + 4));
        }

        private string ReadLooseKey(int index)
        {
            var offset = LooseRecordOffset(index);
            return ReadBlobString(ReadInt32(offset), ReadInt32(offset + 4));
        }

        private List<string> ReadRows(int index)
        {
            var offset = RecordOffset(index);
            var payload = ReadBlobString(ReadInt32(offset + 8), ReadInt32(offset + 12));
            var result = new List<string>();
            foreach (var row in payload.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                result.Add(row);
            }

            return result;
        }

        private string ReadBlobString(int offset, int length)
        {
            if (offset < 0 || length < 0 || blobOffset + offset + length > data.Length)
            {
                throw new InvalidOperationException("Corrupt dictionary index resource.");
            }

            return Encoding.UTF8.GetString(data, blobOffset + offset, length);
        }

        private int RecordOffset(int index)
        {
            return recordsOffset + checked(index * RecordSize);
        }

        private int LooseRecordOffset(int index)
        {
            return looseOffset + checked(index * LooseRecordSize);
        }

        private int ReadInt32(int offset)
        {
            return data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24);
        }
    }

    internal sealed class LookupResult
    {
        public LookupResult()
        {
            Meanings = new List<string>();
            Synonyms = new List<string>();
            Antonyms = new List<string>();
            Examples = new List<string>();
        }

        public string Query { get; set; }
        public string MatchedTerm { get; set; }
        public bool Found { get; set; }
        public List<string> Meanings { get; private set; }
        public List<string> Synonyms { get; private set; }
        public List<string> Antonyms { get; private set; }
        public List<string> Examples { get; private set; }
    }

    internal sealed class TermEntry
    {
        public TermEntry(string term)
        {
            Term = term;
            Meanings = new List<string>();
            Synonyms = new List<string>();
            Antonyms = new List<string>();
            Examples = new List<string>();
        }

        public string Term { get; private set; }
        public List<string> Meanings { get; private set; }
        public List<string> Synonyms { get; private set; }
        public List<string> Antonyms { get; private set; }
        public List<string> Examples { get; private set; }

        public void AddMeaning(string value)
        {
            AddUnique(Meanings, value);
        }

        public void AddSynonyms(IEnumerable<string> values)
        {
            AddUniqueRange(Synonyms, values);
        }

        public void AddAntonyms(IEnumerable<string> values)
        {
            AddUniqueRange(Antonyms, values);
        }

        public void AddExamples(IEnumerable<string> values)
        {
            AddUniqueRange(Examples, values);
        }

        private static void AddUniqueRange(List<string> target, IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                AddUnique(target, value);
            }
        }

        private static void AddUnique(List<string> target, string value)
        {
            value = value == null ? "" : value.Trim();
            if (value.Length == 0)
            {
                return;
            }

            foreach (var existing in target)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(existing, value))
                {
                    return;
                }
            }

            target.Add(value);
        }
    }

    internal static class TextTools
    {
        public static string NormalizeQuery(string value)
        {
            if (value == null)
            {
                return "";
            }

            value = value.Trim().ToLowerInvariant();
            value = value.Replace('\u2010', '-')
                .Replace('\u2011', '-')
                .Replace('\u2012', '-')
                .Replace('\u2013', '-')
                .Replace('\u2014', '-')
                .Replace('\u2212', '-')
                .Replace('_', ' ');

            while (value.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                value = value.Replace("  ", " ");
            }

            return value;
        }

        public static string LooseKey(string value)
        {
            value = NormalizeQuery(value);
            var builder = new StringBuilder();
            foreach (var ch in value)
            {
                if (ch == '-' || ch == ' ' || ch == '\'' || ch == '.' || ch == '/')
                {
                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString();
        }

        public static List<string> BuildCandidates(string normalized)
        {
            var result = new List<string>();
            AddCandidate(result, normalized);
            AddCandidate(result, normalized.Replace('-', ' '));
            AddCandidate(result, normalized.Replace("-", ""));
            AddCandidate(result, normalized.Replace(" ", "-"));
            AddCandidate(result, normalized.Replace(" ", ""));
            AddCandidate(result, normalized.Replace("'", ""));
            return result;
        }

        private static void AddCandidate(List<string> values, string value)
        {
            value = NormalizeQuery(value);
            if (value.Length == 0)
            {
                return;
            }

            foreach (var existing in values)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(existing, value))
                {
                    return;
                }
            }

            values.Add(value);
        }
    }
}

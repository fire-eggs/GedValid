using GEDWrap;
using SharpGEDParser;
using SharpGEDParser.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using atup = System.Tuple<SharpGEDParser.Model.UnkRec.ErrorCode, string>;
using atup2 = System.Tuple<SharpGEDParser.Model.UnkRec, SharpGEDParser.Model.GEDCommon>;


namespace GedValid
{
    public partial class Form1 : Form
    {
        protected MruStripMenu mnuMRU;

        public event EventHandler LoadGed;

        public Form1()
        {
            InitializeComponent();

            mnuMRU = new MruStripMenuInline(fileToolStripMenuItem, recentFilesToolStripMenuItem, OnMRU);
            mnuMRU.MaxEntries = 7;
            LoadGed += Form1_LoadGed;
            LoadSettings(); // must go after mnuMRU init

        }

        private Dictionary<atup, List<atup2>> _errsByCodeAndTag;
        private Dictionary<Issue.IssueCode, List<Issue>> _issByCode;
        private Dictionary<string, List<atup2>> _unkByTag;

        private class LBItem
        {
            public object data;
            public string disp;
            public int type;
            public object key;
            public override string ToString()
            {
                return disp;
            }
        }

        public const int ERR = 1;
        public const int ISS = 2;
        public const int UNK = 3;

        private void Form1_LoadGed(object sender, EventArgs e)
        {
            //logit("LoadGed 1", true);
            Forest gedtrees = new Forest();
            // TODO Using LastFile is a hack... pass path in args? not as event?            
            gedtrees.LoadGEDCOM(LastFile);
            //logit("LoadGed 2");

            _errsByCodeAndTag = new Dictionary<atup, List<atup2>>();
            _issByCode = new Dictionary<Issue.IssueCode, List<Issue>>();
            _unkByTag = new Dictionary<string, List<atup2>>();

            
            // TODO can't get to reader top-level errors!
            //gedtrees._gedReader.Errors;

            foreach (var record in gedtrees.AllRecords)
            {
                foreach (var err in record.Errors)
                {
                    atup2 t2 = new atup2(err, record);

                    atup t = new atup(err.Error, err.Tag);
                    if (!_errsByCodeAndTag.ContainsKey(t))
                        _errsByCodeAndTag.Add(t, new List<atup2> { t2 });
                    else
                    {
                        _errsByCodeAndTag[t].Add(t2);
                    }
                }

                foreach (var err in record.Unknowns)
                {
                    atup2 t2 = new atup2(err, record);

                    if (!_unkByTag.ContainsKey(err.Tag))
                        _unkByTag.Add(err.Tag, new List<atup2> { t2 });
                    else
                    {
                        _unkByTag[err.Tag].Add(t2);
                    }
                }
            }

            // TODO need the issue<>gedcommon connection
            foreach (var issue in gedtrees.Issues)
            {
                if (!_issByCode.ContainsKey(issue.IssueId))
                    _issByCode.Add(issue.IssueId, new List<Issue>(){issue});
                else
                {
                    _issByCode[issue.IssueId].Add(issue);
                }
            }

            // list of errors
            foreach (var key in _errsByCodeAndTag.Keys)
            {
                LBItem lbi = new LBItem();
                lbi.type = ERR;
                lbi.key = key;
                lbi.data = _errsByCodeAndTag[key];
                lbi.disp = string.Format(".Err:{0} Tag: {1} Count:{2}", key.Item1, key.Item2, _errsByCodeAndTag[key].Count);

                listBox1.Items.Add(lbi);
            }

            // list of issues
            foreach (var iss in _issByCode.Keys)
            {
                LBItem lbi = new LBItem();
                lbi.type = ISS;
                lbi.key = iss;
                lbi.data = _issByCode[iss];
                lbi.disp = string.Format(".Iss:{0} Count:{1}", iss, _issByCode[iss].Count);

                listBox1.Items.Add(lbi);
            }

            // list of unknowns
            foreach (var key in _unkByTag.Keys)
            {
                LBItem lbi = new LBItem();
                lbi.key = key;
                lbi.type = UNK;
                lbi.data = _unkByTag[key];
                lbi.disp = string.Format(".Unk:{0} Count:{1}", key, _unkByTag[key].Count);

                listBox1.Items.Add(lbi);
            }
        }

        private void OnMRU(int number, string filename)
        {
            if (!File.Exists(filename))
            {
                mnuMRU.RemoveFile(number);
                MessageBox.Show("The file no longer exists: " + filename);
                return;
            }

            // TODO process could fail for some reason, in which case remove the file from the MRU list
            LastFile = filename;
            mnuMRU.SetFirstFile(number);
            ProcessGED(filename);
        }

        private void openGEDCOMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            ofd.Filter = "GEDCOM files|*.ged;*.GED";
            ofd.FilterIndex = 1;
            ofd.DefaultExt = "ged";
            ofd.CheckFileExists = true;
            if (DialogResult.OK != ofd.ShowDialog(this))
            {
                return;
            }
            mnuMRU.AddFile(ofd.FileName);
            LastFile = ofd.FileName; // TODO invalid ged file
            ProcessGED(ofd.FileName);
        }

        private void ProcessGED(string gedPath)
        {
            Text = gedPath;

            listBox1.Items.Clear();
            listBox2.Items.Clear();
            richTextBox1.ResetText();
            textBox1.Text = "";

            Application.DoEvents(); // Cycle events so image updates in case GED load/process takes a while
            LoadGed(this, new EventArgs());
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        #region Settings
        private DASettings _mysettings;

        private List<string> _fileHistory = new List<string>();

        private string LastFile
        {
            get
            {
                if (_fileHistory == null || _fileHistory.Count < 1)
                    return null;
                return _fileHistory[0]; // First entry is the most recent
            }
            set
            {
                // Make sure to wipe any older instance
                _fileHistory.Remove(value);
                _fileHistory.Insert(0, value); // First entry is the most recent
            }
        }

        private void LoadSettings()
        {
            _mysettings = DASettings.Load();

            // No existing settings. Use default.
            if (_mysettings.Fake)
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
            else
            {
                // restore windows position
                StartPosition = FormStartPosition.Manual;
                Top = _mysettings.WinTop;
                Left = _mysettings.WinLeft;
                Height = _mysettings.WinHigh;
                Width = _mysettings.WinWide;
                _fileHistory = _mysettings.PathHistory ?? new List<string>();
                _fileHistory.Remove(null);
                mnuMRU.SetFiles(_fileHistory.ToArray());

                LastFile = _mysettings.LastPath;
            }
        }

        private void SaveSettings()
        {
            // TODO check minimized
            var bounds = DesktopBounds;
            _mysettings.WinTop = Location.Y;
            _mysettings.WinLeft = Location.X; 
            _mysettings.WinHigh = bounds.Height;
            _mysettings.WinWide = bounds.Width;
            _mysettings.Fake = false;
            _mysettings.LastPath = LastFile;
            _mysettings.PathHistory = mnuMRU.GetFiles().ToList();
            _mysettings.Save();
        }
        #endregion

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }

        private void printPreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void printSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void printToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private class ListBoxItem2
        {
            public string txt;
            public GEDCommon rec;
            public UnkRec err;
            public Issue iss;
            public override string ToString()
            {
                return txt;
            }
        }

        // NOTE: reading to record would be faster if during the parse, the relative position of each record was remembered
        // NOTE: using record position may be required to cope with line break behavior

        private GEDCommon _lastrec; // Avoid re-reading the same record
        private List<string> _lines;

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 1. have a record
            // 2. get lines from file for the record
            // 3. determine range of lines for the error
            // 4. for each line from file:
            // 4a. if line is within error range
            // 4ai. set format to Red
            // 4aii. else
            // 4aiii. set format to black
            // 4b. append text
            // 4c. append environment.newline

            ListBoxItem2 lbi = listBox2.SelectedItem as ListBoxItem2;

            int beg = lbi.rec.BegLine;
            int end = lbi.rec.EndLine;

            if (_lastrec != lbi.rec)
            {
                _lastrec = lbi.rec;
                _lines = ReadLineSet(beg, end);
            }

            int errbeg = lbi.err.Beg;
            int errend = lbi.err.End;

            richTextBox1.Clear();

            int errLoc = 0; // track location of error lines to insure visibility
            for (int i = beg; i <= end; i++)
            {
                string lin = _lines[i - beg];
                if (i >= errbeg && i <= errend)
                {
                    richTextBox1.SelectionColor = Color.Red;
                    errLoc = richTextBox1.SelectionStart;
                }
                else
                    richTextBox1.SelectionColor = Color.Black;

                richTextBox1.AppendText(lin);
                richTextBox1.AppendText(Environment.NewLine);
            }

            targetVCenter(errbeg-beg, errend-beg);
            //richTextBox1.SelectionStart = errLoc; // insure error lines are visible
            //richTextBox1.ScrollToCaret();

        }


        private List<string> ReadLineSet(int beg, int end)
        {
            List<string> outl = new List<string>();

            using (Stream stream = File.Open(LastFile, FileMode.Open))
            {
                using (StreamReader sr = new StreamReader(stream))
                {
                    for (int i = 1; i < beg; i++)
                        sr.ReadLine();
                    for (int i = beg; i <= end; i++)
                        outl.Add(sr.ReadLine());
                }
            }
            return outl;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var obj = listBox1.SelectedItem as LBItem;

            listBox2.Items.Clear(); // TODO delay update?
            richTextBox1.ResetText();

            if (obj.type != ISS)
            {
                var foo = obj.data as List<atup2>;
                atup2 first = foo[0];
                if (obj.type == ERR)
                    textBox1.Text = string.Format("Error: {0}, Tag:{2}, Count:{1}", first.Item1.Error, foo.Count, first.Item1.Tag);
                else
                    textBox1.Text = string.Format("Unknown: {0}, Count:{1}", first.Item1.Tag, foo.Count);

                foreach (var tuple in foo)
                {
                    UnkRec unk = tuple.Item1;
                    string val = string.Format("Lines:{0}-{1}", unk.Beg, unk.End);
                    ListBoxItem2 lbi2 = new ListBoxItem2();
                    lbi2.txt = val;
                    lbi2.err = unk;
                    lbi2.rec = tuple.Item2;
                    listBox2.Items.Add(lbi2);
                }
            }
            else
            {
                var foo = obj.data as List<Issue>;
                Issue first = foo[0];
                textBox1.Text = string.Format("Issue: {0}, Count:{1}", first.IssueId, foo.Count);
                foreach (var iss in foo)
                {
                    string val = iss.Message();
                    ListBoxItem2 lbi2 = new ListBoxItem2();
                    lbi2.txt = val;
                    lbi2.rec = null; // TODO need record link
                    lbi2.iss = iss;
                    listBox2.Items.Add(lbi2);
                }
            }
        }

        private int lineHeight()
        {
            // Calculate the vertical height of a richtextbox in lines

            //Get the height of the text area.

            int height = TextRenderer.MeasureText(richTextBox1.Text, richTextBox1.Font).Height;

            //rate = visible height / Total height.

            float rate = (1.0f * richTextBox1.Height) / height;

            //Get visible lines.

            int visibleLines = (int)(richTextBox1.Lines.Length * rate);
            return visibleLines;
        }

        private void targetVCenter(int beg, int end)
        {
            // Desire to have a set of lines in vertical center of textbox
            int high = lineHeight();
            float target = beg + (end - beg)/2.0f;
            float targetTop = target - high/2.0f;
            if (targetTop < 0)
                return; // can't position above top

            int dex = richTextBox1.GetFirstCharIndexFromLine((int) targetTop);
            richTextBox1.SelectionStart = dex; 
            richTextBox1.ScrollToCaret();
        }
    }
}

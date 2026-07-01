using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Pasha
{
    // WinShot 風のタブ付き操作ウィンドウ
    internal class MainForm : Form
    {
        private readonly AppController _ctrl;

        // キャプチャタブ
        private RadioButton _rdoSave, _rdoClip;
        private ComboBox _cmbDelay, _cmbMonitor;

        // 基本設定タブ
        private TextBox _txtFolder, _txtPrefix;
        private ComboBox _cmbNameMode, _cmbFormat;
        private NumericUpDown _numDigits, _numQuality;
        private Label _lblDigits, _lblQuality;

        // ホットキータブ
        private readonly Dictionary<string, HotkeyBox> _hkBoxes = new Dictionary<string, HotkeyBox>();

        // 動作設定タブ
        private CheckBox _chkStartInTray, _chkMinToTray, _chkAlsoClip, _chkOpenFolder, _chkSound, _chkNotify;

        private Button _btnApply;
        private Timer _applyFlash;

        private static readonly int[] DelaySecs = { 0, 3, 5, 10 };
        private const int FormW = 500;
        private const string ApplyText = "保存して反映";

        public MainForm(AppController ctrl)
        {
            _ctrl = ctrl;
            BuildUI();
            LoadFromConfig(ctrl.Config);
        }

        private void BuildUI()
        {
            Text = AppInfo.Name + " - スクリーンキャプチャ";
            Icon = IconFactory.Make();
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Yu Gothic UI", 9);
            ClientSize = new Size(FormW, 512);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildCaptureTab());
            tabs.TabPages.Add(BuildBasicTab());
            tabs.TabPages.Add(BuildHotkeyTab());
            tabs.TabPages.Add(BuildBehaviorTab());
            tabs.TabPages.Add(BuildAboutTab());

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48 };

            var btnExit = new Button
            {
                Text = "アプリを終了", Left = 12, Top = 10,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 2, 8, 2)
            };
            btnExit.Click += delegate { _ctrl.ExitApp(); };

            // 右側は自動サイズの FlowLayoutPanel で右寄せ(文字が切れないように)
            var right = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false, Padding = new Padding(0, 8, 8, 8)
            };
            var btnClose = new Button
            {
                Text = "閉じる", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(6, 0, 0, 0), Padding = new Padding(10, 2, 10, 2)
            };
            btnClose.Click += delegate { Close(); };
            _btnApply = new Button
            {
                Text = ApplyText, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(6, 0, 0, 0), Padding = new Padding(10, 2, 10, 2)
            };
            _btnApply.Click += delegate { ApplySettings(); };
            right.Controls.Add(btnClose);   // 右端
            right.Controls.Add(_btnApply);  // その左

            bottom.Controls.Add(right);
            bottom.Controls.Add(btnExit);

            Controls.Add(tabs);
            Controls.Add(bottom);
        }

        // 折り返し対応の説明ラベル
        private Label Note(string text, int left, int top, int width)
        {
            return new Label
            {
                Left = left,
                Top = top,
                AutoSize = true,
                MaximumSize = new Size(width, 0),
                Text = text
            };
        }

        private static Label Cap(string text, int left, int top)
        {
            return new Label { Text = text, Left = left, Top = top, AutoSize = true };
        }

        // ===== キャプチャタブ =====
        private TabPage BuildCaptureTab()
        {
            var p = new TabPage("キャプチャ");

            p.Controls.Add(Cap("出力先:", 16, 18));
            _rdoSave = new RadioButton { Text = "ファイルに保存", Left = 88, Top = 15, AutoSize = true, Checked = true };
            _rdoClip = new RadioButton { Text = "クリップボードへコピー", Left = 210, Top = 15, AutoSize = true };
            p.Controls.Add(_rdoSave);
            p.Controls.Add(_rdoClip);

            p.Controls.Add(Cap("遅延:", 16, 52));
            _cmbDelay = new ComboBox { Left = 88, Top = 48, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbDelay.Items.AddRange(new object[] { "遅延なし", "3 秒後", "5 秒後", "10 秒後" });
            p.Controls.Add(_cmbDelay);

            p.Controls.Add(Cap("モニター:", 16, 84));
            _cmbMonitor = new ComboBox { Left = 88, Top = 80, Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            FillMonitors();
            p.Controls.Add(_cmbMonitor);

            int y = 122;
            p.Controls.Add(MakeCaptureButton("デスクトップ全体をキャプチャ", y, CaptureMode.Desktop, false)); y += 44;
            p.Controls.Add(MakeCaptureButton("アクティブウィンドウをキャプチャ", y, CaptureMode.ActiveWindow, false)); y += 44;
            p.Controls.Add(MakeCaptureButton("クライアント領域をキャプチャ", y, CaptureMode.ClientArea, false)); y += 44;
            p.Controls.Add(MakeCaptureButton("矩形範囲を指定してキャプチャ", y, CaptureMode.Region, false)); y += 44;
            p.Controls.Add(MakeCaptureButton("選択中のモニターをキャプチャ", y, CaptureMode.Monitor, true)); y += 52;

            p.Controls.Add(Note(
                "※「アクティブウィンドウ」「クライアント領域」は、遅延を設定して対象ウィンドウを前面にしてから撮るか、ホットキーでの撮影が確実です。",
                16, y, FormW - 50));
            return p;
        }

        private Button MakeCaptureButton(string text, int top, CaptureMode mode, bool useMonitor)
        {
            var b = new Button { Text = text, Left = 16, Top = top, Width = 340, Height = 36 };
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Click += delegate
            {
                OutputKind output = _rdoClip.Checked ? OutputKind.Clipboard : OutputKind.Save;
                int delay = DelaySecs[Math.Max(0, _cmbDelay.SelectedIndex)];
                int monitor = useMonitor ? _cmbMonitor.SelectedIndex : -1;
                _ctrl.Capture(mode, output, monitor, delay);
            };
            return b;
        }

        private void FillMonitors()
        {
            _cmbMonitor.Items.Clear();
            var screens = Screen.AllScreens;
            int primary = 0;
            for (int i = 0; i < screens.Length; i++)
            {
                var s = screens[i];
                _cmbMonitor.Items.Add(string.Format("モニター {0} ({1}x{2}){3}",
                    i + 1, s.Bounds.Width, s.Bounds.Height, s.Primary ? " [メイン]" : ""));
                if (s.Primary) primary = i;
            }
            if (_cmbMonitor.Items.Count > 0) _cmbMonitor.SelectedIndex = primary;
        }

        // ===== 基本設定タブ =====
        private TabPage BuildBasicTab()
        {
            var p = new TabPage("基本設定");
            int y = 16;

            p.Controls.Add(Cap("保存先フォルダー:", 16, y)); y += 24;
            _txtFolder = new TextBox { Left = 16, Top = y, Width = 380 };
            var btn = new Button { Left = 402, Top = y - 1, Width = 92, Text = "参照..." };
            btn.Click += delegate
            {
                using (var d = new FolderBrowserDialog())
                {
                    if (Directory.Exists(_txtFolder.Text)) d.SelectedPath = _txtFolder.Text;
                    if (d.ShowDialog() == DialogResult.OK) _txtFolder.Text = d.SelectedPath;
                }
            };
            p.Controls.Add(_txtFolder); p.Controls.Add(btn); y += 40;

            p.Controls.Add(Cap("ファイル名の接頭語:", 16, y)); y += 24;
            _txtPrefix = new TextBox { Left = 16, Top = y, Width = 220 };
            p.Controls.Add(_txtPrefix); y += 40;

            p.Controls.Add(Cap("ファイル名の形式:", 16, y)); y += 24;
            _cmbNameMode = new ComboBox { Left = 16, Top = y, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbNameMode.Items.AddRange(new object[] { "連番 (接頭語 + 番号)", "日時 (接頭語 + 日付時刻)", "なし (接頭語のみ)" });
            _cmbNameMode.SelectedIndexChanged += delegate { UpdateEnabled(); };
            p.Controls.Add(_cmbNameMode);
            _lblDigits = Cap("連番の桁数:", 252, y + 3);
            p.Controls.Add(_lblDigits);
            _numDigits = new NumericUpDown { Left = 340, Top = y, Width = 60, Minimum = 1, Maximum = 8, Value = 4 };
            p.Controls.Add(_numDigits); y += 40;

            p.Controls.Add(Cap("保存形式:", 16, y)); y += 24;
            _cmbFormat = new ComboBox { Left = 16, Top = y, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbFormat.Items.AddRange(new object[] { "PNG", "JPEG" });
            _cmbFormat.SelectedIndexChanged += delegate { UpdateEnabled(); };
            p.Controls.Add(_cmbFormat);
            _lblQuality = Cap("JPEG画質:", 160, y + 3);
            p.Controls.Add(_lblQuality);
            _numQuality = new NumericUpDown { Left = 250, Top = y, Width = 60, Minimum = 1, Maximum = 100, Value = 90 };
            p.Controls.Add(_numQuality); y += 44;

            p.Controls.Add(Note(
                "例: 接頭語「ScreenShot_」+ 連番4桁 → ScreenShot_0001.png\n既存ファイルを調べて次の番号を自動で付与します。",
                16, y, FormW - 50));
            return p;
        }

        // ===== ホットキータブ =====
        private TabPage BuildHotkeyTab()
        {
            var p = new TabPage("ホットキー");
            var desc = Note(
                "各機能の欄をクリックして割り当てたいキーを押してください（Ctrl / Shift / Alt との組み合わせ推奨）。Delete キーで解除、空欄の機能は無効です。",
                12, 10, FormW - 36);
            p.Controls.Add(desc);

            int y = desc.Bottom + 14;
            foreach (var a in HotkeyActionDef.All())
            {
                p.Controls.Add(new Label { Text = a.Label, Left = 16, Top = y + 4, Width = 280, AutoSize = false });
                var box = new HotkeyBox { Left = 300, Top = y, Width = 192 };
                // 欄を編集中だけホットキーを一時解除(欄がキーを拾えるように)
                box.Enter += delegate { _ctrl.SuspendHotkeys(); };
                box.Leave += delegate
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (!AnyHotkeyBoxFocused()) _ctrl.ResumeHotkeys();
                    });
                };
                _hkBoxes[a.Id] = box;
                p.Controls.Add(box);
                y += 34;
            }
            return p;
        }

        private bool AnyHotkeyBoxFocused()
        {
            foreach (var b in _hkBoxes.Values) if (b.Focused) return true;
            return false;
        }

        // ===== 動作設定タブ =====
        private TabPage BuildBehaviorTab()
        {
            var p = new TabPage("動作設定");
            int y = 18;
            _chkStartInTray = AddCheck(p, "起動時はトレイに格納する（操作画面を表示しない）", ref y);
            _chkMinToTray = AddCheck(p, "「閉じる」やウィンドウの × でトレイに格納する", ref y);
            _chkAlsoClip = AddCheck(p, "ファイル保存時にクリップボードへもコピーする", ref y);
            _chkOpenFolder = AddCheck(p, "保存後に保存先フォルダーを開く", ref y);
            _chkSound = AddCheck(p, "撮影時にシャッター音を鳴らす", ref y);
            _chkNotify = AddCheck(p, "撮影・起動時に通知を表示する", ref y);
            return p;
        }

        private CheckBox AddCheck(TabPage p, string text, ref int y)
        {
            var c = new CheckBox { Text = text, Left = 16, Top = y, AutoSize = true };
            p.Controls.Add(c);
            y += 32;
            return c;
        }

        // ===== 情報タブ =====
        private TabPage BuildAboutTab()
        {
            var p = new TabPage("情報");
            p.Controls.Add(new Label
            {
                Left = 16, Top = 20, AutoSize = true,
                Font = new Font("Yu Gothic UI", 14, FontStyle.Bold),
                Text = AppInfo.Name + "  v" + AppInfo.Version
            });
            p.Controls.Add(Cap("WinShot 風のスクリーンキャプチャ常駐ツール", 16, 64));
            p.Controls.Add(Cap("作者: " + AppInfo.Author, 16, 92));
            p.Controls.Add(Cap("ライセンス: " + AppInfo.License, 16, 116));

            var link = new LinkLabel { Left = 16, Top = 144, AutoSize = true, Text = AppInfo.Url };
            link.Click += delegate
            {
                try { System.Diagnostics.Process.Start(AppInfo.Url); } catch { }
            };
            p.Controls.Add(link);

            var btnFolder = new Button { Left = 16, Top = 184, Width = 200, AutoSize = true, Text = "保存先フォルダーを開く" };
            btnFolder.Click += delegate { _ctrl.OpenSaveFolder(); };
            p.Controls.Add(btnFolder);

            p.Controls.Add(Note(
                "本ソフトは WinShot へのリスペクトに基づく独自実装であり、オリジナルの WinShot とは無関係です。",
                16, 234, FormW - 50));
            return p;
        }

        // ===== 設定の読み込み / 反映 =====
        private void LoadFromConfig(AppConfig c)
        {
            _cmbDelay.SelectedIndex = DelayIndex(c.Delay);
            _txtFolder.Text = c.SaveFolder;
            _txtPrefix.Text = c.Prefix;
            _cmbNameMode.SelectedIndex = c.NameMode == FileNameMode.DateTime ? 1
                                       : c.NameMode == FileNameMode.PrefixOnly ? 2 : 0;
            _numDigits.Value = Clamp(c.SequenceDigits, 1, 8);
            _cmbFormat.SelectedItem = c.Format == "JPEG" ? "JPEG" : "PNG";
            _numQuality.Value = Clamp(c.JpegQuality, 1, 100);

            foreach (var kv in _hkBoxes)
                kv.Value.SetInitial(c.GetHotkey(kv.Key));

            _chkStartInTray.Checked = c.StartInTray;
            _chkMinToTray.Checked = c.MinimizeToTrayOnClose;
            _chkAlsoClip.Checked = c.AlsoClipboardOnSave;
            _chkOpenFolder.Checked = c.OpenFolderAfterSave;
            _chkSound.Checked = c.PlaySound;
            _chkNotify.Checked = c.ShowNotification;

            UpdateEnabled();
        }

        private void ApplySettings()
        {
            var c = _ctrl.Config.Clone();
            c.Delay = DelaySecs[Math.Max(0, _cmbDelay.SelectedIndex)];
            c.SaveFolder = _txtFolder.Text.Trim();
            c.Prefix = _txtPrefix.Text;
            c.NameMode = _cmbNameMode.SelectedIndex == 1 ? FileNameMode.DateTime
                       : _cmbNameMode.SelectedIndex == 2 ? FileNameMode.PrefixOnly
                       : FileNameMode.Sequence;
            c.SequenceDigits = (int)_numDigits.Value;
            c.Format = (string)_cmbFormat.SelectedItem == "JPEG" ? "JPEG" : "PNG";
            c.JpegQuality = (int)_numQuality.Value;

            foreach (var kv in _hkBoxes)
                c.Hotkeys[kv.Key] = kv.Value.Value.Clone();

            c.StartInTray = _chkStartInTray.Checked;
            c.MinimizeToTrayOnClose = _chkMinToTray.Checked;
            c.AlsoClipboardOnSave = _chkAlsoClip.Checked;
            c.OpenFolderAfterSave = _chkOpenFolder.Checked;
            c.PlaySound = _chkSound.Checked;
            c.ShowNotification = _chkNotify.Checked;

            var failed = _ctrl.ApplyConfig(c);   // この時点で即時反映

            if (failed.Count > 0)
            {
                MessageBox.Show(this,
                    "次のホットキーは他のアプリ等と競合しているため登録できませんでした:\n\n  " +
                    string.Join("\n  ", failed.ToArray()) +
                    "\n\n別のキーに変更してください。",
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                FlashApplied();
            }
        }

        // 保存ボタンに一瞬「保存しました」と表示して反映を知らせる(通知は出さない)
        private void FlashApplied()
        {
            _btnApply.Text = "保存しました";
            if (_applyFlash == null)
            {
                _applyFlash = new Timer { Interval = 1200 };
                _applyFlash.Tick += delegate { _applyFlash.Stop(); _btnApply.Text = ApplyText; };
            }
            _applyFlash.Stop();
            _applyFlash.Start();
        }

        private void UpdateEnabled()
        {
            bool seq = _cmbNameMode.SelectedIndex == 0;
            _lblDigits.Enabled = seq;
            _numDigits.Enabled = seq;
            bool jpeg = (string)_cmbFormat.SelectedItem == "JPEG";
            _lblQuality.Enabled = jpeg;
            _numQuality.Enabled = jpeg;
        }

        private static int DelayIndex(int sec)
        {
            for (int i = 0; i < DelaySecs.Length; i++) if (DelaySecs[i] == sec) return i;
            return 0;
        }

        private static decimal Clamp(int v, int lo, int hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        // ×ボタン: 設定に応じてトレイ格納 or 終了
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                if (_ctrl.Config.MinimizeToTrayOnClose)
                {
                    Hide();
                    _ctrl.ResumeHotkeys(); // 念のためトレイ格納中はホットキー有効に
                }
                else
                    _ctrl.ExitApp();
            }
            base.OnFormClosing(e);
        }
    }
}

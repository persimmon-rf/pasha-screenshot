using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Pasha
{
    // アプリ全体の制御: トレイ常駐 / ホットキー / キャプチャ実行
    internal class AppController : ApplicationContext
    {
        private AppConfig _cfg;
        private readonly NotifyIcon _tray;
        private readonly HotkeyWindow _hk;
        private readonly List<HotkeyActionDef> _actions;
        private readonly Dictionary<int, HotkeyActionDef> _idMap = new Dictionary<int, HotkeyActionDef>();
        private MainForm _form;
        private string _lastSavedPath;
        private bool _suspended;   // 操作ウィンドウ前面時はホットキーを一時解除
        private bool _mainActive;  // 操作ウィンドウが前面か

        public AppConfig Config { get { return _cfg; } }

        public AppController()
        {
            _cfg = AppConfig.Load();
            _actions = HotkeyActionDef.All();

            _hk = new HotkeyWindow();
            _hk.HotkeyPressed += OnHotkey;
            RegisterHotkeys();

            _tray = new NotifyIcon
            {
                Icon = IconFactory.Make(),
                Text = AppInfo.Name + " - スクリーンキャプチャ",
                Visible = true,
                ContextMenuStrip = BuildTrayMenu()
            };
            _tray.DoubleClick += delegate { ShowMain(); };
            _tray.BalloonTipClicked += OnBalloonClicked;

            _form = new MainForm(this);
            if (!_cfg.StartInTray) ShowMain();

            if (_cfg.ShowNotification)
                _tray.ShowBalloonTip(2500, AppInfo.Name + " 起動",
                    "タスクトレイに常駐しました。アイコンのダブルクリックで操作画面を表示します。", ToolTipIcon.Info);
        }

        // ===== メインウィンドウ =====
        public void ShowMain()
        {
            if (_form == null || _form.IsDisposed) _form = new MainForm(this);
            _form.Show();
            if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
            _form.Activate();
            _form.BringToFront();
        }

        // ===== ホットキー =====
        // 設定変更後に呼ぶと再登録する。登録に失敗した(競合した)ラベル一覧を返す。
        public List<string> RegisterHotkeys()
        {
            var failed = new List<string>();
            _hk.UnregisterAll();
            _idMap.Clear();
            for (int i = 0; i < _actions.Count; i++)
            {
                var a = _actions[i];
                var def = _cfg.GetHotkey(a.Id);
                if (!def.IsSet) continue;
                int id = i + 1;
                if (_hk.Register(id, def.Mods, def.Vk)) _idMap[id] = a;
                else failed.Add(a.Label + " (" + def.ToDisplay() + ")");
            }
            _suspended = false;
            return failed;
        }

        // 操作ウィンドウが前面のときはホットキーを一時解除して、
        // 割り当て欄でキー入力(Ctrl+Shift+1 等)を拾えるようにする。
        // ウィンドウが非前面(他アプリ作業中)のときだけホットキーを有効化。
        public void NotifyMainActive(bool active)
        {
            _mainActive = active;
            if (active) SuspendHotkeys();
            else ResumeHotkeys();
        }

        private void SuspendHotkeys()
        {
            if (_suspended) return;
            _hk.UnregisterAll();
            _idMap.Clear();
            _suspended = true;
        }

        private void ResumeHotkeys()
        {
            if (!_suspended) return;
            RegisterHotkeys();
        }

        private void OnHotkey(int id)
        {
            HotkeyActionDef a;
            if (_idMap.TryGetValue(id, out a))
                Capture(a.Mode, a.Output, -1, _cfg.Delay);
        }

        // ===== トレイメニュー =====
        private ContextMenuStrip BuildTrayMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(Item("操作画面を表示", delegate { ShowMain(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item("デスクトップ全体を保存", delegate { Capture(CaptureMode.Desktop, OutputKind.Save, -1, _cfg.Delay); }));
            menu.Items.Add(Item("アクティブウィンドウを保存", delegate { Capture(CaptureMode.ActiveWindow, OutputKind.Save, -1, _cfg.Delay); }));
            menu.Items.Add(Item("クライアント領域を保存", delegate { Capture(CaptureMode.ClientArea, OutputKind.Save, -1, _cfg.Delay); }));
            menu.Items.Add(Item("矩形範囲を指定して保存", delegate { Capture(CaptureMode.Region, OutputKind.Save, -1, 0); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item("保存先フォルダーを開く", delegate { OpenSaveFolder(); }));
            menu.Items.Add(Item("設定...", delegate { ShowMain(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item("終了", delegate { ExitApp(); }));
            return menu;
        }

        private static ToolStripMenuItem Item(string text, EventHandler onClick)
        {
            var it = new ToolStripMenuItem(text);
            it.Click += onClick;
            return it;
        }

        // ===== キャプチャ =====
        public void Capture(CaptureMode mode, OutputKind output, int monitorIndex, int delaySeconds)
        {
            if (_tray.ContextMenuStrip != null) _tray.ContextMenuStrip.Close();

            int delay = (mode == CaptureMode.Region) ? 0 : delaySeconds;
            if (delay > 0)
            {
                if (_cfg.ShowNotification)
                    _tray.ShowBalloonTip(1200, AppInfo.Name, delay + " 秒後に撮影します...", ToolTipIcon.Info);
                var t = new Timer { Interval = delay * 1000 };
                t.Tick += delegate
                {
                    t.Stop();
                    t.Dispose();
                    DoCapture(mode, output, monitorIndex);
                };
                t.Start();
                return;
            }
            DoCapture(mode, output, monitorIndex);
        }

        private void DoCapture(CaptureMode mode, OutputKind output, int monitorIndex)
        {
            bool wasVisible = _form != null && !_form.IsDisposed && _form.Visible &&
                              _form.WindowState != FormWindowState.Minimized;
            try
            {
                if (wasVisible)
                {
                    _form.Hide();
                    System.Threading.Thread.Sleep(180);
                    Application.DoEvents();
                }
                Bitmap bmp = Grab(mode, monitorIndex);
                if (bmp == null) return;
                using (bmp) Output(bmp, output);
            }
            catch (Exception ex)
            {
                _tray.ShowBalloonTip(3000, AppInfo.Name + " エラー", ex.Message, ToolTipIcon.Error);
            }
            finally
            {
                if (wasVisible && _form != null && !_form.IsDisposed)
                {
                    _form.Show();
                    _form.Activate();
                }
            }
        }

        private Bitmap Grab(CaptureMode mode, int monitorIndex)
        {
            switch (mode)
            {
                case CaptureMode.Desktop:
                    return CaptureEngine.CaptureRect(SystemInformation.VirtualScreen);
                case CaptureMode.ActiveWindow:
                    return CaptureEngine.CaptureActiveWindow();
                case CaptureMode.ClientArea:
                    return CaptureEngine.CaptureClientArea();
                case CaptureMode.Monitor:
                    var screens = Screen.AllScreens;
                    if (monitorIndex >= 0 && monitorIndex < screens.Length)
                        return CaptureEngine.CaptureRect(screens[monitorIndex].Bounds);
                    return null;
                case CaptureMode.Region:
                    using (var sel = new RegionSelectForm())
                    {
                        if (sel.ShowDialog() != DialogResult.OK) return null;
                        var r = sel.Selection;
                        if (r.Width < 1 || r.Height < 1) return null;
                        return CaptureEngine.CaptureRect(r);
                    }
                default:
                    return null;
            }
        }

        private void Output(Bitmap bmp, OutputKind output)
        {
            if (output == OutputKind.Clipboard)
            {
                try { Clipboard.SetImage(bmp); } catch { }
                _lastSavedPath = null;
                Notify("クリップボードにコピーしました");
            }
            else
            {
                string path = SaveWithRetry(bmp);
                if (_cfg.AlsoClipboardOnSave)
                    try { Clipboard.SetImage(bmp); } catch { }
                _lastSavedPath = path;
                Notify("保存しました: " + Path.GetFileName(path));
                if (_cfg.OpenFolderAfterSave) SelectInExplorer(path);
            }
            if (_cfg.PlaySound)
                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
        }

        private string BuildSavePath()
        {
            Directory.CreateDirectory(_cfg.SaveFolder);
            string ext = _cfg.Format == "JPEG" ? ".jpg" : ".png";

            if (_cfg.NameMode == FileNameMode.Sequence)
            {
                int next = NextSequence(ext);
                string path;
                do
                {
                    string num = next.ToString("D" + _cfg.SequenceDigits);
                    path = Path.Combine(_cfg.SaveFolder, _cfg.Prefix + num + ext);
                    next++;
                } while (File.Exists(path));
                return path;
            }

            string baseName;
            if (_cfg.NameMode == FileNameMode.DateTime)
                baseName = _cfg.Prefix + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            else // PrefixOnly: 接頭語のみ
                baseName = _cfg.Prefix.Length > 0 ? _cfg.Prefix : "screenshot";

            return MakeUniquePath(baseName, ext);
        }

        // 同名ファイルがある場合は _1, _2... と自動で連番を付けて衝突を回避
        private string MakeUniquePath(string baseName, string ext)
        {
            string path = Path.Combine(_cfg.SaveFolder, baseName + ext);
            int n = 1;
            while (File.Exists(path))
                path = Path.Combine(_cfg.SaveFolder, baseName + "_" + (n++) + ext);
            return path;
        }

        // 書き込みに失敗(ファイルロック等)しても別名で再試行し、極力エラーにしない
        private string SaveWithRetry(Bitmap bmp)
        {
            string path = BuildSavePath();
            for (int attempt = 0; attempt < 6; attempt++)
            {
                try { SaveImage(bmp, path); return path; }
                catch
                {
                    if (attempt == 5) throw;
                    string dir = Path.GetDirectoryName(path);
                    string name = Path.GetFileNameWithoutExtension(path);
                    string ext = Path.GetExtension(path);
                    path = Path.Combine(dir, name + "_" + (attempt + 1) + ext);
                }
            }
            return path;
        }

        private int NextSequence(string ext)
        {
            int max = 0;
            try
            {
                foreach (var f in Directory.GetFiles(_cfg.SaveFolder, _cfg.Prefix + "*" + ext))
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (name.Length <= _cfg.Prefix.Length) continue;
                    string numPart = name.Substring(_cfg.Prefix.Length);
                    int val;
                    if (IsAllDigits(numPart) && int.TryParse(numPart, out val) && val > max)
                        max = val;
                }
            }
            catch { }
            return max + 1;
        }

        private static bool IsAllDigits(string s)
        {
            if (s.Length == 0) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        private void SaveImage(Bitmap bmp, string path)
        {
            if (_cfg.Format == "JPEG")
            {
                var enc = GetEncoder(ImageFormat.Jpeg);
                var ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(Encoder.Quality, (long)_cfg.JpegQuality);
                bmp.Save(path, enc, ep);
            }
            else
            {
                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.FormatID == format.Guid) return c;
            return null;
        }

        private void Notify(string msg)
        {
            if (_cfg.ShowNotification)
                _tray.ShowBalloonTip(2500, AppInfo.Name, msg, ToolTipIcon.Info);
        }

        private void OnBalloonClicked(object sender, EventArgs e)
        {
            if (_lastSavedPath != null && File.Exists(_lastSavedPath))
                SelectInExplorer(_lastSavedPath);
        }

        private static void SelectInExplorer(string path)
        {
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch { }
        }

        public void OpenSaveFolder()
        {
            try
            {
                Directory.CreateDirectory(_cfg.SaveFolder);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + _cfg.SaveFolder + "\"");
            }
            catch { }
        }

        // ===== 設定の適用 =====
        // 失敗(競合)したホットキーのラベル一覧を返す
        public List<string> ApplyConfig(AppConfig newCfg)
        {
            _cfg = newCfg;
            _cfg.Save();
            var failed = RegisterHotkeys();          // 競合検出のため一旦登録
            if (_mainActive) SuspendHotkeys();       // ウィンドウ前面中は解除状態に戻す
            _tray.ContextMenuStrip = BuildTrayMenu();
            return failed;
        }

        public void ExitApp()
        {
            _tray.Visible = false;
            _hk.Dispose();
            _tray.Dispose();
            if (_form != null && !_form.IsDisposed) _form.Dispose();
            ExitThread();
        }
    }
}

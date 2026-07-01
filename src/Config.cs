using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Pasha
{
    internal enum CaptureMode { Desktop, ActiveWindow, ClientArea, Region, Monitor }
    internal enum OutputKind { Save, Clipboard }
    internal enum FileNameMode { Sequence, DateTime, PrefixOnly }

    // ホットキー1件分の定義 (RegisterHotKey 用)
    internal class HotkeyDef
    {
        // 修飾キー: ALT=1, CONTROL=2, SHIFT=4, WIN=8
        public uint Mods;
        public uint Vk;

        public bool IsSet { get { return Vk != 0; } }

        public HotkeyDef() { }
        public HotkeyDef(uint mods, uint vk) { Mods = mods; Vk = vk; }

        public HotkeyDef Clone() { return new HotkeyDef(Mods, Vk); }

        public string ToConfig()
        {
            if (!IsSet) return "";
            return Mods.ToString() + ":" + Vk.ToString();
        }

        public static HotkeyDef ParseConfig(string s)
        {
            var d = new HotkeyDef();
            if (string.IsNullOrEmpty(s)) return d;
            var parts = s.Split(':');
            if (parts.Length == 2)
            {
                uint m, v;
                if (uint.TryParse(parts[0], out m) && uint.TryParse(parts[1], out v))
                {
                    d.Mods = m;
                    d.Vk = v;
                }
            }
            return d;
        }

        public string ToDisplay()
        {
            if (!IsSet) return "(なし)";
            var sb = new StringBuilder();
            if ((Mods & 2) != 0) sb.Append("Ctrl + ");
            if ((Mods & 4) != 0) sb.Append("Shift + ");
            if ((Mods & 1) != 0) sb.Append("Alt + ");
            if ((Mods & 8) != 0) sb.Append("Win + ");
            sb.Append(KeyName(Vk));
            return sb.ToString();
        }

        public static string KeyName(uint vk)
        {
            Keys k = (Keys)vk;
            if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (k - Keys.D0))).ToString();
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num" + (k - Keys.NumPad0);
            if (k >= Keys.F1 && k <= Keys.F24) return k.ToString();
            switch (k)
            {
                case Keys.PrintScreen: return "PrintScreen";
                case Keys.Insert: return "Insert";
                case Keys.Delete: return "Delete";
                case Keys.Home: return "Home";
                case Keys.End: return "End";
                case Keys.PageUp: return "PageUp";
                case Keys.PageDown: return "PageDown";
                case Keys.Space: return "Space";
                default: return k.ToString();
            }
        }
    }

    // ホットキー割り当て対象の定義 (キャプチャ対象 × 出力先)
    internal class HotkeyActionDef
    {
        public string Id;
        public string Label;
        public CaptureMode Mode;
        public OutputKind Output;

        public HotkeyActionDef(string id, string label, CaptureMode mode, OutputKind output)
        {
            Id = id; Label = label; Mode = mode; Output = output;
        }

        // 並び順が RegisterHotKey の ID(1..N) に対応する
        public static List<HotkeyActionDef> All()
        {
            var list = new List<HotkeyActionDef>();
            list.Add(new HotkeyActionDef("Desktop_Save", "デスクトップ全体 → 保存", CaptureMode.Desktop, OutputKind.Save));
            list.Add(new HotkeyActionDef("Active_Save", "アクティブウィンドウ → 保存", CaptureMode.ActiveWindow, OutputKind.Save));
            list.Add(new HotkeyActionDef("Client_Save", "クライアント領域 → 保存", CaptureMode.ClientArea, OutputKind.Save));
            list.Add(new HotkeyActionDef("Region_Save", "矩形範囲指定 → 保存", CaptureMode.Region, OutputKind.Save));
            list.Add(new HotkeyActionDef("Desktop_Clip", "デスクトップ全体 → クリップボード", CaptureMode.Desktop, OutputKind.Clipboard));
            list.Add(new HotkeyActionDef("Active_Clip", "アクティブウィンドウ → クリップボード", CaptureMode.ActiveWindow, OutputKind.Clipboard));
            list.Add(new HotkeyActionDef("Client_Clip", "クライアント領域 → クリップボード", CaptureMode.ClientArea, OutputKind.Clipboard));
            list.Add(new HotkeyActionDef("Region_Clip", "矩形範囲指定 → クリップボード", CaptureMode.Region, OutputKind.Clipboard));
            return list;
        }
    }

    internal class AppConfig
    {
        public string SaveFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Pasha");
        public string Prefix = "ScreenShot_";
        public FileNameMode NameMode = FileNameMode.Sequence;
        public int SequenceDigits = 4;
        public string Format = "PNG";        // PNG / JPEG
        public int JpegQuality = 90;
        public int Delay = 0;                 // 秒 (0/3/5/10)
        public bool AlsoClipboardOnSave = false;
        public bool PlaySound = true;
        public bool ShowNotification = false;  // デスクトップ通知は既定オフ
        public bool StartInTray = false;
        public bool MinimizeToTrayOnClose = true;
        public bool OpenFolderAfterSave = false;
        public Dictionary<string, HotkeyDef> Hotkeys = DefaultHotkeys();

        private static Dictionary<string, HotkeyDef> DefaultHotkeys()
        {
            const uint CTRL = 2, SHIFT = 4, ALT = 1;
            var d = new Dictionary<string, HotkeyDef>();
            d["Desktop_Save"] = new HotkeyDef(CTRL | SHIFT, (uint)Keys.D1);
            d["Active_Save"] = new HotkeyDef(CTRL | SHIFT, (uint)Keys.D2);
            d["Client_Save"] = new HotkeyDef(CTRL | SHIFT, (uint)Keys.D4);
            d["Region_Save"] = new HotkeyDef(CTRL | SHIFT, (uint)Keys.D3);
            d["Desktop_Clip"] = new HotkeyDef(CTRL | ALT, (uint)Keys.D1);
            d["Active_Clip"] = new HotkeyDef(CTRL | ALT, (uint)Keys.D2);
            d["Client_Clip"] = new HotkeyDef(CTRL | ALT, (uint)Keys.D4);
            d["Region_Clip"] = new HotkeyDef(CTRL | ALT, (uint)Keys.D3);
            return d;
        }

        public HotkeyDef GetHotkey(string id)
        {
            HotkeyDef d;
            if (Hotkeys.TryGetValue(id, out d) && d != null) return d;
            return new HotkeyDef();
        }

        public AppConfig Clone()
        {
            var c = new AppConfig
            {
                SaveFolder = SaveFolder,
                Prefix = Prefix,
                NameMode = NameMode,
                SequenceDigits = SequenceDigits,
                Format = Format,
                JpegQuality = JpegQuality,
                Delay = Delay,
                AlsoClipboardOnSave = AlsoClipboardOnSave,
                PlaySound = PlaySound,
                ShowNotification = ShowNotification,
                StartInTray = StartInTray,
                MinimizeToTrayOnClose = MinimizeToTrayOnClose,
                OpenFolderAfterSave = OpenFolderAfterSave,
                Hotkeys = new Dictionary<string, HotkeyDef>()
            };
            foreach (var kv in Hotkeys) c.Hotkeys[kv.Key] = kv.Value.Clone();
            return c;
        }

        private static string ConfigPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pasha");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.ini");
            }
        }

        public static AppConfig Load()
        {
            var c = new AppConfig();
            try
            {
                if (!File.Exists(ConfigPath)) return c;
                foreach (var raw in File.ReadAllLines(ConfigPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();

                    if (k.StartsWith("HK_"))
                    {
                        c.Hotkeys[k.Substring(3)] = HotkeyDef.ParseConfig(v);
                        continue;
                    }
                    switch (k)
                    {
                        case "SaveFolder": c.SaveFolder = v; break;
                        case "Prefix": c.Prefix = v; break;
                        case "NameMode":
                            c.NameMode = v == "DateTime" ? FileNameMode.DateTime
                                       : v == "PrefixOnly" ? FileNameMode.PrefixOnly
                                       : FileNameMode.Sequence;
                            break;
                        case "SequenceDigits": int.TryParse(v, out c.SequenceDigits); break;
                        case "Format": c.Format = v.ToUpperInvariant() == "JPEG" ? "JPEG" : "PNG"; break;
                        case "JpegQuality": int.TryParse(v, out c.JpegQuality); break;
                        case "Delay": int.TryParse(v, out c.Delay); break;
                        case "AlsoClipboardOnSave": bool.TryParse(v, out c.AlsoClipboardOnSave); break;
                        case "PlaySound": bool.TryParse(v, out c.PlaySound); break;
                        case "ShowNotification": bool.TryParse(v, out c.ShowNotification); break;
                        case "StartInTray": bool.TryParse(v, out c.StartInTray); break;
                        case "MinimizeToTrayOnClose": bool.TryParse(v, out c.MinimizeToTrayOnClose); break;
                        case "OpenFolderAfterSave": bool.TryParse(v, out c.OpenFolderAfterSave); break;
                    }
                }
            }
            catch { }
            if (c.JpegQuality < 1 || c.JpegQuality > 100) c.JpegQuality = 90;
            if (c.SequenceDigits < 1 || c.SequenceDigits > 8) c.SequenceDigits = 4;
            return c;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Pasha 設定ファイル (自動生成)");
                sb.AppendLine("SaveFolder=" + SaveFolder);
                sb.AppendLine("Prefix=" + Prefix);
                sb.AppendLine("NameMode=" + NameMode);
                sb.AppendLine("SequenceDigits=" + SequenceDigits);
                sb.AppendLine("Format=" + Format);
                sb.AppendLine("JpegQuality=" + JpegQuality);
                sb.AppendLine("Delay=" + Delay);
                sb.AppendLine("AlsoClipboardOnSave=" + AlsoClipboardOnSave);
                sb.AppendLine("PlaySound=" + PlaySound);
                sb.AppendLine("ShowNotification=" + ShowNotification);
                sb.AppendLine("StartInTray=" + StartInTray);
                sb.AppendLine("MinimizeToTrayOnClose=" + MinimizeToTrayOnClose);
                sb.AppendLine("OpenFolderAfterSave=" + OpenFolderAfterSave);
                foreach (var kv in Hotkeys)
                    sb.AppendLine("HK_" + kv.Key + "=" + kv.Value.ToConfig());
                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}

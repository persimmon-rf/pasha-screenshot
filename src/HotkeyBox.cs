using System;
using System.Windows.Forms;

namespace Pasha
{
    // ホットキーをキー入力で割り当てる読み取り専用テキストボックス
    internal class HotkeyBox : TextBox
    {
        public HotkeyDef Value = new HotkeyDef();
        public event EventHandler ValueChanged;

        public HotkeyBox()
        {
            ReadOnly = true;
            Cursor = Cursors.Hand;
            ShortcutsEnabled = false;
            Text = Value.ToDisplay();
        }

        public void SetInitial(HotkeyDef d)
        {
            Value = d != null ? d.Clone() : new HotkeyDef();
            Text = Value.ToDisplay();
        }

        private void Commit(HotkeyDef d)
        {
            Value = d;
            Text = d.ToDisplay();
            var h = ValueChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;

            Keys key = e.KeyCode;
            // クリア
            if (key == Keys.Back || key == Keys.Delete)
            {
                Commit(new HotkeyDef());
                return;
            }
            // 修飾キー単独は無視 (組み合わせるキーを待つ)
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
                key == Keys.LWin || key == Keys.RWin)
                return;
            // PrintScreen は KeyUp で処理する
            if (key == Keys.PrintScreen) return;

            uint mods = 0;
            if (e.Control) mods |= 2;
            if (e.Shift) mods |= 4;
            if (e.Alt) mods |= 1;

            bool allowNoModifier = (key >= Keys.F1 && key <= Keys.F24);
            if (mods == 0 && !allowNoModifier) return; // 誤爆防止: 修飾キー必須

            Commit(new HotkeyDef(mods, (uint)key));
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            // PrintScreen は KeyDown が来ないため KeyUp で拾う
            if (e.KeyCode == Keys.PrintScreen)
            {
                uint mods = 0;
                if (e.Control) mods |= 2;
                if (e.Shift) mods |= 4;
                if (e.Alt) mods |= 1;
                Commit(new HotkeyDef(mods, (uint)Keys.PrintScreen));
                e.Handled = true;
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            e.Handled = true; // 文字入力を抑止
        }
    }
}

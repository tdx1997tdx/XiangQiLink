using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VinXiangQi
{
    // 方案管理：新建方案（在保存方案窗口里用瞄准器选窗口）、删除方案
    public partial class Mainform
    {
        void InitWindowPicker()
        {
            button_get_hwnd.Text = "新建连线方案";
        }

        private void button_get_hwnd_Click(object sender, EventArgs e)
        {
            ShowSolutionDialog(true);
        }

        // 打开保存方案窗口；保存了新方案就自动选中它
        void ShowSolutionDialog(bool startBlank)
        {
            var before = new HashSet<string>(SolutionList.Keys);
            string prevSelected = comboBox_solution.SelectedItem as string;
            SolutionSavingForm ssf = new SolutionSavingForm();
            ssf.StartBlank = startBlank;
            ssf.StartPosition = FormStartPosition.CenterParent;
            ssf.ShowDialog(this);
            InitSolutions();
            string added = SolutionList.Keys.FirstOrDefault(k => !before.Contains(k));
            if (added != null)
            {
                comboBox_solution.SelectedItem = added;
                DisplayStatus("已保存并选中方案：" + added + "，点「一键连线」开始");
            }
            else if (prevSelected != null && comboBox_solution.Items.Contains(prevSelected))
            {
                comboBox_solution.SelectedItem = prevSelected;
            }
        }

        // 删除方案：挪到 Solutions\_已删除\ 下（可恢复），不真删
        private void button_delete_solution_Click(object sender, EventArgs e)
        {
            string name = comboBox_solution.SelectedItem as string;
            if (string.IsNullOrEmpty(name) || name == "自动全屏" || !SolutionList.ContainsKey(name))
            {
                MessageBox.Show(this, "先在下拉框里选中要删除的方案（「自动全屏」是内置的，不能删）");
                return;
            }
            if (MessageBox.Show(this, "删除方案「" + name + "」？\n（会移到 Solutions\\_已删除 文件夹，需要时可以挪回来）", "删除方案", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            try
            {
                string src = Path.Combine(SOLUTION_FOLDER, name);
                string trash = Path.Combine(SOLUTION_FOLDER, "_已删除");
                Directory.CreateDirectory(trash);
                string dst = Path.Combine(trash, name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.Move(src, dst);
                LogHelper.Log("删除方案: " + name + " -> " + dst);
            }
            catch (Exception ex)
            {
                LogHelper.Log("删除方案失败: " + name + " " + ex.Message);
                MessageBox.Show(this, "删除失败：" + ex.Message);
                return;
            }
            Settings.SelectedSolution = "自动全屏";
            InitSolutions();
            comboBox_solution.SelectedItem = "自动全屏";
            DisplayStatus("已删除方案：" + name);
        }
    }

    // 拖拽选窗口时盖在目标窗口上的红框：鼠标穿透、不抢焦点、不出现在任务栏
    class WindowHighlightForm : Form
    {
        public WindowHighlightForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08000000; // LAYERED | TRANSPARENT | TOOLWINDOW | NOACTIVATE
                return cp;
            }
        }

        public void ShowAt(Rectangle r)
        {
            Bounds = r;
            if (!Visible) Show();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var pen = new Pen(Color.Red, 6))
            {
                e.Graphics.DrawRectangle(pen, 3, 3, ClientSize.Width - 6, ClientSize.Height - 6);
            }
        }
    }

    // 瞄准器：按住拖到目标窗口上松开。用定时器轮询鼠标位置和左键状态，不依赖鼠标捕获（按钮等控件会抢捕获）
    public class WindowFinderBox : Label
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        // 拖动中鼠标下的窗口变化时触发（顶层窗口，子窗口）；不可选时为 Zero
        public event Action<IntPtr, IntPtr> Hovering;
        // 松开时触发；没落在可选窗口上时 top 为 Zero
        public event Action<IntPtr, IntPtr> Picked;

        readonly Timer timer = new Timer { Interval = 30 };
        WindowHighlightForm highlight;
        IntPtr curTop = IntPtr.Zero, curChild = IntPtr.Zero;
        bool dragging = false;

        public WindowFinderBox()
        {
            Text = "⊕";
            TextAlign = ContentAlignment.MiddleCenter;
            Font = new Font("Segoe UI Symbol", 22F);
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = Color.White;
            Cursor = Cursors.Cross;
            timer.Tick += Timer_Tick;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || dragging) return;
            dragging = true;
            curTop = IntPtr.Zero;
            curChild = IntPtr.Zero;
            Text = "";
            if (highlight == null || highlight.IsDisposed) highlight = new WindowHighlightForm();
            timer.Start();
        }

        void Timer_Tick(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.Cross;
            bool leftDown = (GetAsyncKeyState(0x01) & 0x8000) != 0;
            if (!leftDown)
            {
                Finish();
                return;
            }
            Point pt = Cursor.Position;
            IntPtr child = ScreenshotHelper.WindowFromPoint(pt.X, pt.Y);
            IntPtr top = child == IntPtr.Zero ? IntPtr.Zero : GetAncestor(child, 2);
            uint pid;
            if (top != IntPtr.Zero && GetWindowThreadProcessId(top, out pid) != 0 && pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id)
            {
                top = IntPtr.Zero;
                child = IntPtr.Zero;
            }
            if (top != curTop)
            {
                curTop = top;
                Hovering?.Invoke(top, child);
                try
                {
                    if (top == IntPtr.Zero) highlight.Hide();
                    else highlight.ShowAt(ScreenshotHelper.GetWindowRectangle(top));
                }
                catch (Exception ex)
                {
                    LogHelper.Log("瞄准器红框显示失败: " + ex);
                }
            }
            curChild = child;
        }

        void Finish()
        {
            timer.Stop();
            dragging = false;
            Text = "⊕";
            if (highlight != null && !highlight.IsDisposed) highlight.Hide();
            Picked?.Invoke(curTop, curChild);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop();
                timer.Dispose();
                if (highlight != null && !highlight.IsDisposed) highlight.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

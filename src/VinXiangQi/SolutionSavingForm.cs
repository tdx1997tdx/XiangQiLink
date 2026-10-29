using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace VinXiangQi
{
    public partial class SolutionSavingForm : Form
    {
        public string GameTitle;
        public string GameClass;
        public string ClickTitle;
        public string ClickClass;
        // 新建方案：不预填当前连线窗口的信息，用瞄准器选
        public bool StartBlank = false;
        public SolutionSavingForm()
        {
            InitializeComponent();
            windowFinder.Hovering += (top, child) =>
            {
                label_finder.Text = top == IntPtr.Zero
                    ? "拖到游戏窗口上（本程序的窗口不能选）"
                    : "松开选中：" + WindowDesc(top);
            };
            windowFinder.Picked += WindowFinder_Picked;
        }

        static string WindowDesc(IntPtr hwnd)
        {
            string title = ScreenshotHelper.GetWindowTitle(hwnd);
            return (string.IsNullOrEmpty(title) ? "(无标题)" : title) + "  [" + ScreenshotHelper.GetWindowClass(hwnd) + "]";
        }

        // 截图用顶层窗口；鼠标下是子窗口时，点击用子窗口
        private void WindowFinder_Picked(IntPtr top, IntPtr child)
        {
            if (top == IntPtr.Zero)
            {
                label_finder.Text = "没选中：要在其他程序的窗口上松开。再拖一次试试";
                return;
            }
            textBox_game_title.Text = ScreenshotHelper.GetWindowTitle(top);
            textBox_game_class.Text = ScreenshotHelper.GetWindowClass(top);
            if (child != IntPtr.Zero && child != top)
            {
                textBox_click_title.Text = ScreenshotHelper.GetWindowTitle(child);
                textBox_click_class.Text = ScreenshotHelper.GetWindowClass(child);
            }
            else
            {
                textBox_click_title.Text = "";
                textBox_click_class.Text = "";
            }
            if (textBox_name.Text.Trim() == "")
            {
                textBox_name.Text = textBox_game_title.Text.Trim() != "" ? textBox_game_title.Text.Trim() : textBox_game_class.Text;
            }
            label_finder.Text = "已选中：" + WindowDesc(top) + (child != top && child != IntPtr.Zero ? "\n点击窗口：[" + ScreenshotHelper.GetWindowClass(child) + "]" : "");
            LogHelper.Log("保存方案-瞄准器选中: " + WindowDesc(top) + " 子窗口=" + (child != top ? WindowDesc(child) : "无"));
        }

        void InitUI()
        {
            if (StartBlank) return;
            GameTitle = ScreenshotHelper.GetWindowTitle(Mainform.GameHandle);
            GameClass = ScreenshotHelper.GetWindowClass(Mainform.GameHandle);
            if (Mainform.GameHandle != Mainform.ClickHandle)
            {
                ClickTitle = ScreenshotHelper.GetWindowTitle(Mainform.ClickHandle);
                ClickClass = ScreenshotHelper.GetWindowClass(Mainform.ClickHandle);
                textBox_click_title.Text = ClickTitle;
                textBox_click_class.Text = ClickClass;
            }
            textBox_game_title.Text = GameTitle;
            textBox_game_class.Text = GameClass;
            textBox_name.Text = GameTitle;
        }

        private void button_save_settings_Click(object sender, EventArgs e)
        {
            if (textBox_name.Text.Trim() == "" || (textBox_game_title.Text.Trim() == "" && textBox_game_class.Text.Trim() == ""))
            {
                MessageBox.Show(this, "先用瞄准器选一个窗口，并填好方案名");
                return;
            }
            if (textBox_name.Text.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || textBox_name.Text.Trim().StartsWith("_"))
            {
                MessageBox.Show(this, "方案名里不能有 \\ / : * ? \" < > | 这些字符，也不能以 _ 开头");
                return;
            }
            string folder = Mainform.SOLUTION_FOLDER + textBox_name.Text;
            folder = folder.Trim();
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(folder + "\\" + "window.txt", 
                $"截图标题={textBox_game_title.Text}\r\n" +
                $"截图类={textBox_game_class.Text}\r\n" +
                $"点击标题={textBox_click_title.Text}\r\n" +
                $"点击类={textBox_click_class.Text}"
                );
            this.Close();
        }

        private void SolutionSavingForm_Load(object sender, EventArgs e)
        {
            InitUI();
        }
    }
}

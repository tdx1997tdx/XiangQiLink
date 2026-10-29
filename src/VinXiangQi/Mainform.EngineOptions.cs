using System;
using System.Windows.Forms;

namespace VinXiangQi
{
    // 主界面「引擎设置」里的哈希表和「更多参数」
    public partial class Mainform
    {
        private void numericUpDown_hash_ValueChanged(object sender, EventArgs e)
        {
            string hashStr = numericUpDown_hash.Value.ToString();
            if (Settings.HashSize == (int)numericUpDown_hash.Value && Engine != null && Engine.Initialized)
            {
                return;
            }
            Settings.HashSize = (int)numericUpDown_hash.Value;
            if (Engine != null && Engine.Initialized)
            {
                // 引擎会等当前这步算完再重新分配哈希表
                Engine.SetOption("Hash", hashStr);
                LogHelper.Log("设置哈希表: " + hashStr + "MB");
            }
            if (Settings.CurrentEngine != null)
            {
                Settings.CurrentEngine.Configs["Hash"] = hashStr;
            }
            SaveSettings();
        }

        // 打开当前引擎的全部参数（引擎自己报告的 option 列表），改动立即发给正在运行的引擎并保存
        private void button_engine_options_Click(object sender, EventArgs e)
        {
            if (Engine == null || !Engine.Initialized || Settings.CurrentEngine == null)
            {
                MessageBox.Show(this, "引擎还没加载");
                return;
            }
            var form = new EngineSettingsForm(Engine, Settings.CurrentEngine);
            form.StartPosition = FormStartPosition.CenterParent;
            form.Text = Settings.SelectedEngine + " 全部参数";
            form.ShowDialog(this);
            // 线程数、哈希表以主界面为准：把对话框里的改动同步回来
            var configs = Settings.CurrentEngine.Configs;
            int v;
            if (configs.ContainsKey("Threads") && int.TryParse(configs["Threads"], out v))
            {
                numericUpDown_thread_count.Value = Math.Max(numericUpDown_thread_count.Minimum, Math.Min(numericUpDown_thread_count.Maximum, v));
            }
            if (configs.ContainsKey("Hash") && int.TryParse(configs["Hash"], out v))
            {
                numericUpDown_hash.Value = Math.Max(numericUpDown_hash.Minimum, Math.Min(numericUpDown_hash.Maximum, v));
            }
        }
    }
}

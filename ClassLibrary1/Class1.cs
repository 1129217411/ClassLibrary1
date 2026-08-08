using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ClassLibrary1
{
    public class MainForm : Form
    {
        private Panel leftPanel;
        private Panel rightPanel;
        private Button btnMenuEmulator;
        private Button btnMenuSettings;

        // 模拟器设置页
        private Panel pageFunction;
        // 功能设置页
        private Panel pageSettings;

        private Color menuNormal = Color.FromArgb(45, 45, 48);
        private Color menuActive = Color.FromArgb(0, 122, 204);
        private Color sidebarBg = Color.FromArgb(37, 37, 38);

        public MainForm()
        {
            this.Text = "畅玩冬日";
            this.Size = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(700, 400);

            BuildUI();
        }

        private void BuildUI()
        {
            // 使用 TableLayoutPanel 做主布局（左侧固定160，右侧填充）
            TableLayoutPanel mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 2;
            mainLayout.RowCount = 1;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
            mainLayout.Margin = new Padding(0);
            mainLayout.Padding = new Padding(0);
            this.Controls.Add(mainLayout);

            // === 左侧导航栏 ===
            leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Fill;
            leftPanel.BackColor = sidebarBg;
            mainLayout.Controls.Add(leftPanel, 0, 0);

            Label lblLogo = new Label();
            lblLogo.Text = "畅玩冬日";
            lblLogo.Font = new Font("Microsoft YaHei", 12, FontStyle.Bold);
            lblLogo.ForeColor = Color.White;
            lblLogo.Location = new Point(15, 20);
            lblLogo.AutoSize = true;
            leftPanel.Controls.Add(lblLogo);

            Panel separator = new Panel();
            separator.Location = new Point(15, 55);
            separator.Size = new Size(130, 1);
            separator.BackColor = Color.FromArgb(80, 80, 80);
            leftPanel.Controls.Add(separator);

            btnMenuEmulator = MakeMenuBtn("模拟器设置", 70);
            btnMenuEmulator.Click += (s, e) => ShowPage("function");
            leftPanel.Controls.Add(btnMenuEmulator);

            btnMenuSettings = MakeMenuBtn("功能设置", 115);
            btnMenuSettings.Click += (s, e) => ShowPage("settings");
            leftPanel.Controls.Add(btnMenuSettings);

            Button btnExit = MakeMenuBtn("退出", 0);
            btnExit.Dock = DockStyle.Bottom;
            btnExit.Click += BtnExit_Click;
            leftPanel.Controls.Add(btnExit);

            // === 右侧内容区 ===
            rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Fill;
            rightPanel.BackColor = Color.White;
            mainLayout.Controls.Add(rightPanel, 1, 0);

            // 创建页面
            BuildFunctionPage();
            BuildSettingsPage();

            // 默认显示模拟器设置
            ShowPage("function");
        }

        private Button MakeMenuBtn(string text, int y)
        {
            Button btn = new Button();
            btn.Text = "  " + text;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            if (y > 0) btn.Location = new Point(0, y);
            btn.Size = new Size(160, 40);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Microsoft YaHei", 10f);
            btn.ForeColor = Color.White;
            btn.BackColor = menuNormal;
            btn.Cursor = Cursors.Hand;
            btn.MouseEnter += (s, e) => { if (btn.BackColor != menuActive) btn.BackColor = Color.FromArgb(60, 60, 65); };
            btn.MouseLeave += (s, e) => { if (btn.BackColor != menuActive) btn.BackColor = menuNormal; };
            return btn;
        }

        private void BuildSettingsPage()
        {
            pageSettings = new Panel();
            pageSettings.Dock = DockStyle.Fill;
            pageSettings.BackColor = Color.White;
            pageSettings.Visible = false;

            Label title = new Label();
            title.Text = "功能设置";
            title.Font = new Font("Microsoft YaHei", 16, FontStyle.Bold);
            title.Location = new Point(30, 25);
            title.AutoSize = true;
            pageSettings.Controls.Add(title);

            rightPanel.Controls.Add(pageSettings);
        }

        private string dnconsolePath;
        private System.Collections.Generic.List<string> allConsolePaths
            = new System.Collections.Generic.List<string>();
        private System.Collections.Generic.Dictionary<int, string> instanceConsoleMap
            = new System.Collections.Generic.Dictionary<int, string>();
        private Panel emulatorListPanel;
        private System.Collections.Generic.HashSet<int> selectedInstances = new System.Collections.Generic.HashSet<int>();
        private System.Collections.Generic.Dictionary<int, CheckBox> checkBoxes
            = new System.Collections.Generic.Dictionary<int, CheckBox>();
        private System.Collections.Generic.Dictionary<int, Label> statusLabels
            = new System.Collections.Generic.Dictionary<int, Label>();
        private System.Windows.Forms.Timer statusTimer;
        private Button btnLaunchSelected;

        private void BuildFunctionPage()
        {
            pageFunction = new Panel();
            pageFunction.Dock = DockStyle.Fill;
            pageFunction.BackColor = Color.White;
            pageFunction.Visible = false;

            Label title = new Label();
            title.Text = "模拟器管理";
            title.Font = new Font("Microsoft YaHei", 16, FontStyle.Bold);
            title.Location = new Point(30, 20);
            title.AutoSize = true;
            pageFunction.Controls.Add(title);

            // 右上角刷新按钮
            Button btnRefresh = new Button();
            btnRefresh.Text = "刷新";
            btnRefresh.Size = new Size(60, 30);
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.FlatAppearance.BorderSize = 1;
            btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            btnRefresh.BackColor = Color.White;
            btnRefresh.ForeColor = Color.FromArgb(60, 60, 60);
            btnRefresh.Font = new Font("Microsoft YaHei", 9f);
            btnRefresh.Cursor = Cursors.Hand;
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Click += (s, ev) => { selectedInstances.Clear(); dnconsolePath = FindDnConsole(); BuildEmulatorRows(GetEmulatorInstances()); };
            pageFunction.Controls.Add(btnRefresh);

            // 设置路径按钮
            Button btnSetPath = new Button();
            btnSetPath.Text = "绑定路径";
            btnSetPath.Size = new Size(75, 30);
            btnSetPath.FlatStyle = FlatStyle.Flat;
            btnSetPath.FlatAppearance.BorderSize = 1;
            btnSetPath.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            btnSetPath.BackColor = Color.White;
            btnSetPath.ForeColor = Color.FromArgb(60, 60, 60);
            btnSetPath.Font = new Font("Microsoft YaHei", 9f);
            btnSetPath.Cursor = Cursors.Hand;
            btnSetPath.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSetPath.Click += BtnSetPath_Click;
            pageFunction.Controls.Add(btnSetPath);

            pageFunction.Resize += (s, ev) =>
            {
                btnRefresh.Location = new Point(pageFunction.Width - btnRefresh.Width - 20, 18);
                btnSetPath.Location = new Point(pageFunction.Width - btnRefresh.Width - btnSetPath.Width - 28, 18);
            };

            dnconsolePath = FindDnConsole();

            if (dnconsolePath == null)
            {
                Label lblErr = new Label();
                lblErr.Text = "未找到雷电模拟器控制台";
                lblErr.ForeColor = Color.Red;
                lblErr.Location = new Point(30, 70);
                lblErr.AutoSize = true;
                lblErr.Font = new Font("Microsoft YaHei", 10f);
                pageFunction.Controls.Add(lblErr);
            }
            else
            {
                // 表头：是否启动 | 模拟器 | 运行状态
                Label lblHeader1 = new Label();
                lblHeader1.Text = "是否启动";
                lblHeader1.Location = new Point(30, 65);
                lblHeader1.Size = new Size(80, 25);
                lblHeader1.Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
                lblHeader1.ForeColor = Color.Gray;
                pageFunction.Controls.Add(lblHeader1);

                Label lblHeader2 = new Label();
                lblHeader2.Text = "模拟器";
                lblHeader2.Location = new Point(120, 65);
                lblHeader2.Size = new Size(180, 25);
                lblHeader2.Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
                lblHeader2.ForeColor = Color.Gray;
                pageFunction.Controls.Add(lblHeader2);

                Label lblHeader3 = new Label();
                lblHeader3.Text = "运行状态";
                lblHeader3.Location = new Point(340, 65);
                lblHeader3.Size = new Size(100, 25);
                lblHeader3.Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
                lblHeader3.ForeColor = Color.Gray;
                pageFunction.Controls.Add(lblHeader3);

                Panel sep = new Panel();
                sep.Location = new Point(30, 90);
                sep.Size = new Size(430, 1);
                sep.BackColor = Color.FromArgb(230, 230, 230);
                sep.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                pageFunction.Controls.Add(sep);

                // 表头竖线1（是否启动 | 模拟器）
                Panel vlineHeader1 = new Panel();
                vlineHeader1.Location = new Point(105, 62);
                vlineHeader1.Size = new Size(1, 30);
                vlineHeader1.BackColor = Color.FromArgb(210, 210, 210);
                pageFunction.Controls.Add(vlineHeader1);

                // 表头竖线2（模拟器 | 运行状态）
                Panel vlineHeader2 = new Panel();
                vlineHeader2.Location = new Point(325, 62);
                vlineHeader2.Size = new Size(1, 30);
                vlineHeader2.BackColor = Color.FromArgb(210, 210, 210);
                pageFunction.Controls.Add(vlineHeader2);

                // 模拟器列表容器
                emulatorListPanel = new Panel();
                emulatorListPanel.Location = new Point(0, 92);
                emulatorListPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                emulatorListPanel.AutoScroll = true;
                emulatorListPanel.BackColor = Color.White;
                pageFunction.Controls.Add(emulatorListPanel);

                // 底部按钮区
                Panel bottomPanel = new Panel();
                bottomPanel.Dock = DockStyle.Bottom;
                bottomPanel.Height = 55;
                bottomPanel.BackColor = Color.FromArgb(248, 248, 248);
                pageFunction.Controls.Add(bottomPanel);

                Panel topBorder = new Panel();
                topBorder.Dock = DockStyle.Top;
                topBorder.Height = 1;
                topBorder.BackColor = Color.FromArgb(230, 230, 230);
                bottomPanel.Controls.Add(topBorder);

                btnLaunchSelected = new Button();
                btnLaunchSelected.Text = "启动所选模拟器";
                btnLaunchSelected.Size = new Size(180, 38);
                btnLaunchSelected.Location = new Point(30, 9);
                btnLaunchSelected.FlatStyle = FlatStyle.Flat;
                btnLaunchSelected.FlatAppearance.BorderSize = 0;
                btnLaunchSelected.BackColor = Color.FromArgb(0, 122, 204);
                btnLaunchSelected.ForeColor = Color.White;
                btnLaunchSelected.Font = new Font("Microsoft YaHei", 11f);
                btnLaunchSelected.Cursor = Cursors.Hand;
                btnLaunchSelected.Click += BtnLaunchSelected_Click;
                bottomPanel.Controls.Add(btnLaunchSelected);

                Button btnRunAuto = new Button();
                btnRunAuto.Text = "执行自动化";
                btnRunAuto.Size = new Size(120, 38);
                btnRunAuto.Location = new Point(220, 9);
                btnRunAuto.FlatStyle = FlatStyle.Flat;
                btnRunAuto.FlatAppearance.BorderSize = 0;
                btnRunAuto.BackColor = Color.FromArgb(40, 167, 69);
                btnRunAuto.ForeColor = Color.White;
                btnRunAuto.Font = new Font("Microsoft YaHei", 11f);
                btnRunAuto.Cursor = Cursors.Hand;
                btnRunAuto.Click += (s, ev) => RunAutomationForRunning();
                bottomPanel.Controls.Add(btnRunAuto);

                pageFunction.Resize += (s, ev) =>
                {
                    emulatorListPanel.Size = new Size(pageFunction.Width, pageFunction.Height - 92 - 55);
                };

                // 仅启动时构建一次
                LoadSelectedInstances();
                BuildEmulatorRows(GetEmulatorInstances());

                // 10秒定时刷新运行状态
                statusTimer = new System.Windows.Forms.Timer();
                statusTimer.Interval = 10000;
                statusTimer.Tick += (s, ev) => UpdateStatusOnly();
                statusTimer.Start();

                pageFunction.VisibleChanged += (s, ev) =>
                {
                    if (statusTimer != null)
                    {
                        if (pageFunction.Visible) statusTimer.Start();
                        else statusTimer.Stop();
                    }
                };
            }

            rightPanel.Controls.Add(pageFunction);
        }

        private void BuildEmulatorRows(
            System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> instances)
        {
            if (emulatorListPanel == null || dnconsolePath == null) return;

            emulatorListPanel.Controls.Clear();
            checkBoxes.Clear();
            statusLabels.Clear();

            if (instances.Count == 0)
            {
                Label lblNone = new Label();
                lblNone.Text = "未发现模拟器实例";
                lblNone.ForeColor = Color.Gray;
                lblNone.Location = new Point(30, 20);
                lblNone.AutoSize = true;
                lblNone.Font = new Font("Microsoft YaHei", 10f);
                emulatorListPanel.Controls.Add(lblNone);
                UpdateLaunchButton();
                return;
            }

            int rowY = 5;
            int rowH = 48;

            for (int i = 0; i < instances.Count; i++)
            {
                var inst = instances[i];
                string name = inst.Key;
                int index = inst.Value;
                bool isRunning = GetInstanceStatus(index) == "1";

                Panel row = new Panel();
                row.Location = new Point(0, rowY);
                row.Size = new Size(emulatorListPanel.Width - 20, rowH);
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                row.BackColor = i % 2 == 0 ? Color.White : Color.FromArgb(248, 248, 248);
                emulatorListPanel.Controls.Add(row);

                // 行内竖线1
                Panel vline1 = new Panel();
                vline1.Location = new Point(105, 0);
                vline1.Size = new Size(1, rowH);
                vline1.BackColor = Color.FromArgb(230, 230, 230);
                row.Controls.Add(vline1);

                // 行内竖线2
                Panel vline2 = new Panel();
                vline2.Location = new Point(325, 0);
                vline2.Size = new Size(1, rowH);
                vline2.BackColor = Color.FromArgb(230, 230, 230);
                row.Controls.Add(vline2);

                // 勾选框
                CheckBox chk = new CheckBox();
                chk.Location = new Point(40, 13);
                chk.AutoSize = false;
                chk.Size = new Size(20, 20);
                chk.Checked = selectedInstances.Contains(index);
                chk.Cursor = Cursors.Hand;
                int capturedIndex = index;
                chk.CheckedChanged += (s, ev) =>
                {
                    if (chk.Checked)
                        selectedInstances.Add(capturedIndex);
                    else
                        selectedInstances.Remove(capturedIndex);
                    UpdateLaunchButton();
                    SaveSelectedInstances();
                };
                row.Controls.Add(chk);
                checkBoxes[index] = chk;

                // 模拟器名称
                Label lblName = new Label();
                lblName.Text = name;
                lblName.Location = new Point(115, 13);
                lblName.AutoSize = true;
                lblName.Font = new Font("Microsoft YaHei", 10f);
                row.Controls.Add(lblName);

                // 运行状态标签（始终创建，用于定时更新）
                Label lblStatus = new Label();
                lblStatus.Text = isRunning ? "运行中" : "未启动";
                lblStatus.ForeColor = isRunning ? Color.FromArgb(46, 139, 87) : Color.Gray;
                lblStatus.Location = new Point(340, 13);
                lblStatus.AutoSize = true;
                lblStatus.Font = new Font("Microsoft YaHei", 9f);
                row.Controls.Add(lblStatus);
                statusLabels[index] = lblStatus;

                rowY += rowH;
            }

            UpdateLaunchButton();
        }

        private void UpdateStatusOnly()
        {
            if (statusLabels.Count == 0) return;

            try
            {
                int consoleIdx = 0;
                foreach (string consolePath in allConsolePaths)
                {
                    consoleIdx++;
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = consolePath;
                    psi.Arguments = "list2";
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding("gb2312");
                    psi.CreateNoWindow = true;

                    using (Process p = Process.Start(psi))
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit(3000);

                        foreach (string line in output.Split(new[] { '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string[] parts = line.Split(',');
                            if (parts.Length >= 3)
                            {
                                int idx;
                                if (int.TryParse(parts[0], out idx))
                                {
                                    int uniqueKey = consoleIdx * 1000 + idx;
                                    Label lbl;
                                    if (statusLabels.TryGetValue(uniqueKey, out lbl))
                                    {
                                        bool running = GetRunningStatus(parts) == "1";
                                        lbl.Text = running ? "运行中" : "未启动";
                                        lbl.ForeColor = running ? Color.FromArgb(46, 139, 87) : Color.Gray;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void UpdateLaunchButton()
        {
            if (btnLaunchSelected == null) return;
            int count = selectedInstances.Count;
            if (count == 0)
            {
                btnLaunchSelected.Text = "请先选择模拟器";
                btnLaunchSelected.BackColor = Color.FromArgb(180, 180, 180);
                btnLaunchSelected.Enabled = false;
            }
            else
            {
                btnLaunchSelected.Text = "启动所选模拟器 (" + count + ")";
                btnLaunchSelected.BackColor = Color.FromArgb(0, 122, 204);
                btnLaunchSelected.Enabled = true;
            }
        }

        private void BtnLaunchSelected_Click(object sender, EventArgs e)
        {
            if (selectedInstances.Count == 0) return;

            var launched = new System.Collections.Generic.List<int>(selectedInstances);

            bool fastLaunch = launched.Count < 10;

            foreach (int index in launched)
            {
                // 启动前统一设置分辨率
                SetResolution(index);
                LaunchInstance(index);
                if (!fastLaunch)
                    System.Threading.Thread.Sleep(5000);
            }

            // 保持勾选状态不变
            System.Collections.Generic.HashSet<int> keepSelected
                = new System.Collections.Generic.HashSet<int>(launched);

            // 启动后排列窗口并执行自动化逻辑
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                if (!fastLaunch)
                    System.Threading.Thread.Sleep(5000);
                SortEmulatorWindows();

                // 自动执行每个启动模拟器的自动化逻辑
                foreach (int key in launched)
                {
                    try
                    {
                        string cp = GetConsoleForInstance(key);
                        int ri = GetRealIndex(key);
                        EmulatorHelper helper = new EmulatorHelper(cp, ri);
                        new MyAutomation().Run(helper);
                    }
                    catch { }
                }

                this.BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
                {
                    BuildEmulatorRows(GetEmulatorInstances());
                    // 恢复勾选状态
                    foreach (int key in keepSelected)
                    {
                        if (!selectedInstances.Contains(key))
                            selectedInstances.Add(key);
                    }
                    SaveSelectedInstances();
                    UpdateLaunchButton();
                    foreach (var kv in checkBoxes)
                    {
                        if (selectedInstances.Contains(kv.Key))
                            kv.Value.Checked = true;
                    }
                }));
            });
        }

        private void SortEmulatorWindows()
        {
            // 调用雷电多开器自带的 sortWnd 排列窗口
            foreach (string consolePath in allConsolePaths)
            {
                try
                {
                    ProcessStartInfo sortPsi = new ProcessStartInfo();
                    sortPsi.FileName = consolePath;
                    sortPsi.Arguments = "sortWnd";
                    sortPsi.UseShellExecute = false;
                    sortPsi.CreateNoWindow = true;
                    Process.Start(sortPsi);
                }
                catch { }
            }
        }

        private void SetResolution(int uniqueKey)
        {
            try
            {
                string consolePath = GetConsoleForInstance(uniqueKey);
                int realIndex = GetRealIndex(uniqueKey);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = consolePath;
                psi.Arguments = "modify --index " + realIndex + " --resolution 720,1280,240";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            catch { }
        }

        private string GetRunningStatus(string[] parts)
        {
            // LDPlayer9: idx,name,status,... (status在[2])
            // LDPlayer14: idx,name,pid,pid,status,... (status在[4])
            if (parts.Length >= 5 && (parts[4] == "0" || parts[4] == "1"))
                return parts[4];
            if (parts.Length >= 3 && (parts[2] == "0" || parts[2] == "1"))
                return parts[2];
            return "0";
        }

        // ======================== 自动化入口 ========================
        // 启动后自动为每个模拟器创建 EmulatorHelper 并调用 MyAutomation.Run

        private void RunAutomationForRunning()
        {
            var runningKeys = new System.Collections.Generic.List<int>();
            foreach (int key in selectedInstances)
            {
                if (GetInstanceStatus(key) == "1")
                    runningKeys.Add(key);
            }
            if (runningKeys.Count == 0)
            {
                MessageBox.Show("没有勾选的运行中的模拟器", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (int key in runningKeys)
                {
                    try
                    {
                        string cp = GetConsoleForInstance(key);
                        int ri = GetRealIndex(key);
                        EmulatorHelper helper = new EmulatorHelper(cp, ri);
                        new MyAutomation().Run(helper);
                    }
                    catch { }
                }
            });
        }

        private string GetInstanceStatus(int uniqueKey)
        {
            try
            {
                string consolePath = GetConsoleForInstance(uniqueKey);
                int realIndex = GetRealIndex(uniqueKey);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = consolePath;
                psi.Arguments = "list2";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding("gb2312");
                psi.CreateNoWindow = true;

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);

                    foreach (string line in output.Split(new[] { '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length >= 3)
                        {
                            int idx;
                            if (int.TryParse(parts[0], out idx) && idx == realIndex)
                                return GetRunningStatus(parts);
                        }
                    }
                }
            }
            catch { }
            return "0";
        }

        private void QuitInstance(int uniqueKey)
        {
            try
            {
                string consolePath = GetConsoleForInstance(uniqueKey);
                int realIndex = GetRealIndex(uniqueKey);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = consolePath;
                psi.Arguments = "quit --index " + realIndex;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("关闭失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetEmulatorConfigPath()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            return Path.Combine(dir, "emulator_config.txt");
        }

        private string FindDnConsole()
        {
            allConsolePaths.Clear();
            instanceConsoleMap.Clear();

            // 从 pathconfig.ini 解析所有版本的路径
            string defaultConfig = @"O:\app\雷电\ldmutiplayer\pathconfig.ini";
            FindAllConsolesFromPathConfig(defaultConfig);

            // 从用户配置读取额外路径
            string userConfig = GetEmulatorConfigPath();
            if (File.Exists(userConfig))
            {
                string savedPath = File.ReadAllText(userConfig).Trim();
                if (!string.IsNullOrEmpty(savedPath))
                {
                    if (File.Exists(savedPath) && (savedPath.EndsWith("ldconsole.exe") || savedPath.EndsWith("dnconsole.exe")))
                        AddConsolePath(savedPath);
                    else if (Directory.Exists(savedPath))
                    {
                        string lc = Path.Combine(savedPath, "ldconsole.exe");
                        if (File.Exists(lc)) AddConsolePath(lc);
                        else
                        {
                            string dc = Path.Combine(savedPath, "dnconsole.exe");
                            if (File.Exists(dc)) AddConsolePath(dc);
                        }
                    }
                }
            }

            // 回退：直接查找
            string fallback1 = @"O:\app\雷电\新建文件夹\leidian\LDPlayer9\ldconsole.exe";
            if (File.Exists(fallback1)) AddConsolePath(fallback1);
            string fallback2 = @"O:\app\雷电\leidian\LDPlayer14\ldconsole.exe";
            if (File.Exists(fallback2)) AddConsolePath(fallback2);

            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "diag.txt"),
                DateTime.Now.ToString("[HH:mm:ss] ") + "FindDnConsole: paths=" + allConsolePaths.Count + " result=" + (allConsolePaths.Count > 0 ? allConsolePaths[0] : "null") + "\n",
                System.Text.Encoding.UTF8);

            return allConsolePaths.Count > 0 ? allConsolePaths[0] : null;
        }

        private void FindAllConsolesFromPathConfig(string configPath)
        {
            if (!File.Exists(configPath)) return;
            try
            {
                foreach (string line in File.ReadAllLines(configPath))
                {
                    if (line.StartsWith("player"))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0)
                        {
                            string dir = line.Substring(eq + 1).Trim();
                            string lc = Path.Combine(dir, "ldconsole.exe");
                            if (File.Exists(lc)) AddConsolePath(lc);
                            else
                            {
                                string dc = Path.Combine(dir, "dnconsole.exe");
                                if (File.Exists(dc)) AddConsolePath(dc);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void AddConsolePath(string path)
        {
            if (!allConsolePaths.Contains(path))
                allConsolePaths.Add(path);
        }

        private string FindFromPathConfig(string configPath)
        {
            if (!File.Exists(configPath)) return null;
            try
            {
                foreach (string line in File.ReadAllLines(configPath))
                {
                    if (line.StartsWith("player"))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0)
                        {
                            string dir = line.Substring(eq + 1).Trim();
                            string lc = Path.Combine(dir, "ldconsole.exe");
                            if (File.Exists(lc)) return lc;
                            string dc = Path.Combine(dir, "dnconsole.exe");
                            if (File.Exists(dc)) return dc;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private void BtnSetPath_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "选择模拟器控制台程序 (ldconsole.exe 或 dnconsole.exe)";
                ofd.Filter = "可执行文件|ldconsole.exe;dnconsole.exe|所有文件|*.*";
                ofd.FilterIndex = 1;
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string path = GetEmulatorConfigPath();
                        File.WriteAllText(path, ofd.FileName);
                        dnconsolePath = ofd.FileName;
                        BuildEmulatorRows(GetEmulatorInstances());
                        MessageBox.Show("路径已保存：" + ofd.FileName, "成功",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("保存失败：" + ex.Message, "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> GetEmulatorInstances()
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>();
            instanceConsoleMap.Clear();

            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "diag.txt"),
                DateTime.Now.ToString("[HH:mm:ss] ") + "GetEmulatorInstances called, paths=" + allConsolePaths.Count + "\n",
                System.Text.Encoding.UTF8);

            foreach (string consolePath in allConsolePaths)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = consolePath;
                    psi.Arguments = "list2";
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding("gb2312");
                    psi.CreateNoWindow = true;

                    // 用控制台路径确定版本标签
                    string versionTag = consolePath.Contains("LDPlayer14") ? "A14" : "A9";

                    using (Process p = Process.Start(psi))
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit(3000);

                        System.IO.File.AppendAllText(
                            System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "diag.txt"),
                            DateTime.Now.ToString("[HH:mm:ss] ") + "list2[" + consolePath + "] output=[" + output.Replace("\r", "\\r").Replace("\n", "\\n") + "]\n",
                            System.Text.Encoding.UTF8);

                        // 支持换行和空格分隔（LDPlayer14用空格）
                        foreach (string line in output.Split(new[] { '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string[] parts = line.Split(',');
                            if (parts.Length >= 2)
                            {
                                int index;
                                if (int.TryParse(parts[0], out index))
                                {
                                    string name = parts[1];
                                    int uniqueKey = (allConsolePaths.IndexOf(consolePath) + 1) * 1000 + index;
                                    string displayName = "[" + versionTag + "] " + name;
                                    result.Add(new System.Collections.Generic.KeyValuePair<string, int>(displayName, uniqueKey));
                                    instanceConsoleMap[uniqueKey] = consolePath;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "diag.txt"),
                        DateTime.Now.ToString("[HH:mm:ss] ") + "list2 error: " + ex.Message + "\n",
                        System.Text.Encoding.UTF8);
                }
            }
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "diag.txt"),
                DateTime.Now.ToString("[HH:mm:ss] ") + "GetEmulatorInstances result: " + result.Count + " instances\n",
                System.Text.Encoding.UTF8);
            return result;
        }

        private string GetConsoleForInstance(int uniqueKey)
        {
            string consolePath;
            if (instanceConsoleMap.TryGetValue(uniqueKey, out consolePath))
                return consolePath;
            return dnconsolePath;
        }

        private int GetRealIndex(int uniqueKey)
        {
            return uniqueKey % 1000;
        }

        private void LaunchInstance(int uniqueKey)
        {
            try
            {
                string consolePath = GetConsoleForInstance(uniqueKey);
                int realIndex = GetRealIndex(uniqueKey);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = consolePath;
                psi.Arguments = "launch --index " + realIndex;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowPage(string page)
        {
            pageFunction.Visible = false;
            pageSettings.Visible = false;

            if (page == "function")
            {
                pageFunction.Visible = true;
                pageFunction.BringToFront();
                btnMenuEmulator.BackColor = menuActive;
                btnMenuSettings.BackColor = menuNormal;
            }
            else
            {
                pageSettings.Visible = true;
                pageSettings.BringToFront();
                btnMenuEmulator.BackColor = menuNormal;
                btnMenuSettings.BackColor = menuActive;
            }
        }

        private string GetSelectedEmulatorsPath()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            return Path.Combine(dir, "selected_emulators.txt");
        }

        private void LoadSelectedInstances()
        {
            try
            {
                string path = GetSelectedEmulatorsPath();
                if (!File.Exists(path)) return;
                foreach (string line in File.ReadAllLines(path))
                {
                    int idx;
                    if (int.TryParse(line.Trim(), out idx))
                        selectedInstances.Add(idx);
                }
            }
            catch { }
        }

        private void SaveSelectedInstances()
        {
            try
            {
                string path = GetSelectedEmulatorsPath();
                var lines = new System.Collections.Generic.List<string>();
                foreach (int idx in selectedInstances)
                    lines.Add(idx.ToString());
                File.WriteAllLines(path, lines.ToArray());
            }
            catch { }
        }

        private void BtnExit_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("确定要退出吗？", "提示",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [STAThread]
        public static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "ClassLibrary1_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    // 已有实例在运行，找到它并弹出前台
                    Process current = Process.GetCurrentProcess();
                    foreach (Process p in Process.GetProcessesByName(current.ProcessName))
                    {
                        if (p.Id != current.Id)
                        {
                            ShowWindow(p.MainWindowHandle, 9); // SW_RESTORE
                            SetForegroundWindow(p.MainWindowHandle);
                            break;
                        }
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.Run(new MainForm());
            }
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(370, 347);
            this.Name = "MainForm";
            this.ResumeLayout(false);
        }
    }
}

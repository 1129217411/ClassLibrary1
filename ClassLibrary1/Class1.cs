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
        private Button btnMenu1;
        private Button btnMenu2;

        // 个人信息页控件
        private Panel pageProfile;
        private TextBox txtName;
        private TextBox txtAge;
        private TextBox txtEmail;
        private Label lblProfileMsg;

        // 功能页控件
        private Panel pageFunction;

        private Color menuNormal = Color.FromArgb(45, 45, 48);
        private Color menuActive = Color.FromArgb(0, 122, 204);
        private Color sidebarBg = Color.FromArgb(37, 37, 38);

        public MainForm()
        {
            this.Text = "简单软件页面";
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
            lblLogo.Text = "我的应用";
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

            btnMenu1 = MakeMenuBtn("个人信息", 70);
            btnMenu1.Click += (s, e) => ShowPage("profile");
            leftPanel.Controls.Add(btnMenu1);

            btnMenu2 = MakeMenuBtn("快捷启动", 115);
            btnMenu2.Click += (s, e) => ShowPage("function");
            leftPanel.Controls.Add(btnMenu2);

            Button btnExit = MakeMenuBtn("退出", 0);
            btnExit.Dock = DockStyle.Bottom;
            btnExit.Click += BtnExit_Click;
            leftPanel.Controls.Add(btnExit);

            // === 右侧内容区 ===
            rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Fill;
            rightPanel.BackColor = Color.White;
            mainLayout.Controls.Add(rightPanel, 1, 0);

            // 创建两个页面
            BuildProfilePage();
            BuildFunctionPage();

            // 默认显示个人信息
            ShowPage("profile");

            // 启动时加载已保存的信息
            LoadProfile();
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

        private void BuildProfilePage()
        {
            pageProfile = new Panel();
            pageProfile.Dock = DockStyle.Fill;
            pageProfile.BackColor = Color.White;
            pageProfile.Visible = false;

            Label title = new Label();
            title.Text = "个人信息";
            title.Font = new Font("Microsoft YaHei", 16, FontStyle.Bold);
            title.Location = new Point(30, 25);
            title.AutoSize = true;
            pageProfile.Controls.Add(title);

            int y = 80;
            int gap = 48;
            int lblX = 30;
            int boxX = 110;

            AddRow(pageProfile, "姓名：", lblX, y, boxX, out txtName);
            AddRow(pageProfile, "年龄：", lblX, y + gap, boxX, out txtAge);
            AddRow(pageProfile, "邮箱：", lblX, y + gap * 2, boxX, out txtEmail);

            Button btnSave = new Button();
            btnSave.Text = "保存";
            btnSave.Location = new Point(boxX, y + gap * 3 + 15);
            btnSave.Size = new Size(100, 36);
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.BackColor = Color.FromArgb(0, 122, 204);
            btnSave.ForeColor = Color.White;
            btnSave.Font = new Font("Microsoft YaHei", 10f);
            btnSave.Cursor = Cursors.Hand;
            btnSave.Click += BtnSaveProfile_Click;
            pageProfile.Controls.Add(btnSave);

            lblProfileMsg = new Label();
            lblProfileMsg.Location = new Point(boxX + 115, y + gap * 3 + 23);
            lblProfileMsg.AutoSize = true;
            lblProfileMsg.Font = new Font("Microsoft YaHei", 9f);
            pageProfile.Controls.Add(lblProfileMsg);

            rightPanel.Controls.Add(pageProfile);
        }

        private void AddRow(Panel parent, string label, int lblX, int y, int boxX, out TextBox box)
        {
            Label lbl = new Label();
            lbl.Text = label;
            lbl.Location = new Point(lblX, y + 3);
            lbl.AutoSize = true;
            lbl.Font = new Font("Microsoft YaHei", 10f);
            parent.Controls.Add(lbl);

            box = new TextBox();
            box.Location = new Point(boxX, y);
            box.Size = new Size(300, 28);
            box.Font = new Font("Microsoft YaHei", 10f);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            parent.Controls.Add(box);
        }

        private string dnconsolePath;

        private void BuildFunctionPage()
        {
            pageFunction = new Panel();
            pageFunction.Dock = DockStyle.Fill;
            pageFunction.BackColor = Color.White;
            pageFunction.Visible = false;
            pageFunction.AutoScroll = true;

            Label title = new Label();
            title.Text = "快捷启动";
            title.Font = new Font("Microsoft YaHei", 16, FontStyle.Bold);
            title.Location = new Point(30, 20);
            title.AutoSize = true;
            pageFunction.Controls.Add(title);

            // 查找 dnconsole.exe 路径
            dnconsolePath = FindDnConsole();

            if (dnconsolePath == null)
            {
                Label lblErr = new Label();
                lblErr.Text = "未找到雷电模拟器控制台(dnconsole.exe)";
                lblErr.ForeColor = Color.Red;
                lblErr.Location = new Point(30, 70);
                lblErr.AutoSize = true;
                lblErr.Font = new Font("Microsoft YaHei", 10f);
                pageFunction.Controls.Add(lblErr);

                Button btnManager = new Button();
                btnManager.Text = "打开多开器";
                btnManager.Location = new Point(30, 110);
                btnManager.Size = new Size(140, 38);
                btnManager.FlatStyle = FlatStyle.Flat;
                btnManager.FlatAppearance.BorderSize = 0;
                btnManager.BackColor = Color.FromArgb(100, 100, 100);
                btnManager.ForeColor = Color.White;
                btnManager.Font = new Font("Microsoft YaHei", 10f);
                btnManager.Cursor = Cursors.Hand;
                btnManager.Click += (s, ev) =>
                {
                    string mgr = @"O:\app\雷电\ldmutiplayer\dnmultiplayerex.exe";
                    if (File.Exists(mgr)) Process.Start(mgr);
                };
                pageFunction.Controls.Add(btnManager);
            }
            else
            {
                Label lblApp = new Label();
                lblApp.Text = "选择要启动的模拟器：";
                lblApp.Location = new Point(30, 65);
                lblApp.AutoSize = true;
                lblApp.Font = new Font("Microsoft YaHei", 10f);
                pageFunction.Controls.Add(lblApp);

                // 获取模拟器列表并创建按钮
                var instances = GetEmulatorInstances();
                int btnY = 100;
                int col = 0;
                int btnW = 180;
                int btnH = 40;
                int gapX = 10;
                int gapY = 10;
                Color[] colors = new Color[]
                {
                    Color.FromArgb(46, 139, 87),
                    Color.FromArgb(0, 122, 204),
                    Color.FromArgb(156, 39, 176),
                    Color.FromArgb(255, 152, 0)
                };

                for (int i = 0; i < instances.Count; i++)
                {
                    var inst = instances[i];
                    Button btn = new Button();
                    btn.Text = inst.Key;
                    btn.Location = new Point(30 + col * (btnW + gapX), btnY);
                    btn.Size = new Size(btnW, btnH);
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 0;
                    btn.BackColor = colors[i % colors.Length];
                    btn.ForeColor = Color.White;
                    btn.Font = new Font("Microsoft YaHei", 10f);
                    btn.Cursor = Cursors.Hand;
                    int idx = inst.Value;
                    btn.Click += (s, ev) => LaunchInstance(idx);
                    pageFunction.Controls.Add(btn);

                    col++;
                    if (col >= 2)
                    {
                        col = 0;
                        btnY += btnH + gapY;
                    }
                }

                if (instances.Count == 0)
                {
                    Label lblNone = new Label();
                    lblNone.Text = "未发现模拟器实例";
                    lblNone.ForeColor = Color.Gray;
                    lblNone.Location = new Point(30, 100);
                    lblNone.AutoSize = true;
                    lblNone.Font = new Font("Microsoft YaHei", 10f);
                    pageFunction.Controls.Add(lblNone);
                }
            }

            rightPanel.Controls.Add(pageFunction);
        }

        private string FindDnConsole()
        {
            // 从 pathconfig.ini 读取播放器路径
            string configPath = @"O:\app\雷电\ldmutiplayer\pathconfig.ini";
            if (File.Exists(configPath))
            {
                foreach (string line in File.ReadAllLines(configPath))
                {
                    if (line.StartsWith("player"))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0)
                        {
                            string dir = line.Substring(eq + 1).Trim();
                            string dc = Path.Combine(dir, "dnconsole.exe");
                            if (File.Exists(dc)) return dc;
                        }
                    }
                }
            }
            // 回退：直接查找
            string fallback = @"O:\app\雷电\新建文件夹\leidian\LDPlayer9\dnconsole.exe";
            if (File.Exists(fallback)) return fallback;
            return null;
        }

        private System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> GetEmulatorInstances()
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = dnconsolePath;
                psi.Arguments = "list2";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding("gb2312");
                psi.CreateNoWindow = true;

                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);

                    foreach (string line in output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length >= 2)
                        {
                            int index;
                            if (int.TryParse(parts[0], out index))
                            {
                                string name = parts[1];
                                bool running = parts.Length > 2 && parts[2] == "1";
                                string display = name + (running ? " (运行中)" : "");
                                result.Add(new System.Collections.Generic.KeyValuePair<string, int>(display, index));
                            }
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        private void LaunchInstance(int index)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = dnconsolePath;
                psi.Arguments = "launch --index " + index;
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
            if (page == "profile")
            {
                pageProfile.Visible = true;
                pageFunction.Visible = false;
                pageProfile.BringToFront();
                btnMenu1.BackColor = menuActive;
                btnMenu2.BackColor = menuNormal;
            }
            else
            {
                pageProfile.Visible = false;
                pageFunction.Visible = true;
                pageFunction.BringToFront();
                btnMenu1.BackColor = menuNormal;
                btnMenu2.BackColor = menuActive;
            }
        }

        private string GetDataFilePath()
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            return Path.Combine(dir, "profile.txt");
        }

        private void LoadProfile()
        {
            try
            {
                string path = GetDataFilePath();
                if (!File.Exists(path)) return;

                foreach (string line in File.ReadAllLines(path))
                {
                    int idx = line.IndexOf('=');
                    if (idx < 0) continue;
                    string key = line.Substring(0, idx);
                    string val = line.Substring(idx + 1);
                    switch (key)
                    {
                        case "name": txtName.Text = val; break;
                        case "age": txtAge.Text = val; break;
                        case "email": txtEmail.Text = val; break;
                    }
                }
            }
            catch { }
        }

        private void SaveProfile()
        {
            try
            {
                string path = GetDataFilePath();
                string[] lines = new string[]
                {
                    "name=" + txtName.Text,
                    "age=" + txtAge.Text,
                    "email=" + txtEmail.Text
                };
                File.WriteAllLines(path, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSaveProfile_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                lblProfileMsg.Text = "请至少填写姓名！";
                lblProfileMsg.ForeColor = Color.Red;
            }
            else
            {
                SaveProfile();
                lblProfileMsg.Text = "保存成功！";
                lblProfileMsg.ForeColor = Color.Green;
            }
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

using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace BarcodeRenamer2
{
    public class MainForm : Form
    {
        private AppConfig config;
        private FileScanService scanService;
        private System.Windows.Forms.Timer scanTimer;
        private ScanStatistics totalStats;
        
        // 系统托盘
        private NotifyIcon notifyIcon;
        private ContextMenuStrip trayContextMenu;

        // 控件
        private GroupBox grpConfig;
        private TextBox txtScanFolder;
        private TextBox txtOutputFolder;
        private Button btnBrowseScan;
        private Button btnBrowseOutput;
        private NumericUpDown numInterval;

        private GroupBox grpControl;
        private Button btnStartScan;
        private Button btnStopScan;
        private Button btnScanOnce;
        private Button btnRefresh;

        private GroupBox grpStatistics;
        private Label lblTotalCount;
        private Label lblSuccessCount;
        private Label lblFailedCount;
        private Label lblManualCount;

        private GroupBox grpFileList;
        private ListView lstFiles;
        private Button btnManualReview;

        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;

        public MainForm()
        {
            config = AppConfig.Load();
            scanService = new FileScanService(config);
            totalStats = new ScanStatistics();

            InitializeComponents();
            InitializeTrayIcon(); // 初始化托盘图标
            LoadConfigToUI();
            SetupEventHandlers();
        }

        private void InitializeComponents()
        {
            // 窗体设置
            this.Text = "图片条形码识别重命名工具";
            this.Size = new Size(1000, 700);
            this.MinimumSize = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // 加载自定义图标
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
                else
                {
                    this.Icon = SystemIcons.Information;
                }
            }
            catch
            {
                this.Icon = SystemIcons.Information;
            }

            // 配置区域
            grpConfig = new GroupBox
            {
                Text = "配置",
                Location = new Point(10, 10),
                Size = new Size(450, 120),
                Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold)
            };

            Label lblScanFolder = new Label
            {
                Text = "扫描文件夹:",
                Location = new Point(10, 25),
                Size = new Size(80, 20)
            };

            txtScanFolder = new TextBox
            {
                Location = new Point(95, 23),
                Size = new Size(260, 25)
            };

            btnBrowseScan = new Button
            {
                Text = "浏览...",
                Location = new Point(360, 22),
                Size = new Size(75, 26)
            };

            Label lblOutputFolder = new Label
            {
                Text = "输出文件夹:",
                Location = new Point(10, 55),
                Size = new Size(80, 20)
            };

            txtOutputFolder = new TextBox
            {
                Location = new Point(95, 53),
                Size = new Size(260, 25)
            };

            btnBrowseOutput = new Button
            {
                Text = "浏览...",
                Location = new Point(360, 52),
                Size = new Size(75, 26)
            };

            Label lblInterval = new Label
            {
                Text = "扫描间隔(秒):",
                Location = new Point(10, 85),
                Size = new Size(80, 20)
            };

            numInterval = new NumericUpDown
            {
                Location = new Point(95, 83),
                Size = new Size(100, 25),
                Minimum = 1,
                Maximum = 60,
                Value = config.ScanInterval / 1000
            };

            grpConfig.Controls.AddRange(new Control[] {
                lblScanFolder, txtScanFolder, btnBrowseScan,
                lblOutputFolder, txtOutputFolder, btnBrowseOutput,
                lblInterval, numInterval
            });

            // 控制区域
            grpControl = new GroupBox
            {
                Text = "扫描控制",
                Location = new Point(470, 10),
                Size = new Size(520, 120),
                Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold)
            };

            btnStartScan = new Button
            {
                Text = "开始自动扫描",
                Location = new Point(10, 25),
                Size = new Size(140, 35),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btnStopScan = new Button
            {
                Text = "停止扫描",
                Location = new Point(160, 25),
                Size = new Size(140, 35),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };

            btnScanOnce = new Button
            {
                Text = "单次扫描",
                Location = new Point(310, 25),
                Size = new Size(140, 35),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btnRefresh = new Button
            {
                Text = "刷新列表",
                Location = new Point(310, 70),
                Size = new Size(140, 35),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            grpControl.Controls.AddRange(new Control[] {
                btnStartScan, btnStopScan, btnScanOnce, btnRefresh
            });

            // 统计区域
            grpStatistics = new GroupBox
            {
                Text = "统计数据",
                Location = new Point(10, 140),
                Size = new Size(450, 80),
                Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold)
            };

            lblTotalCount = new Label
            {
                Text = "扫描总数: 0",
                Location = new Point(10, 25),
                Size = new Size(100, 20)
            };

            lblSuccessCount = new Label
            {
                Text = "成功: 0",
                Location = new Point(120, 25),
                Size = new Size(100, 20),
                ForeColor = Color.Green
            };

            lblFailedCount = new Label
            {
                Text = "失败: 0",
                Location = new Point(230, 25),
                Size = new Size(100, 20),
                ForeColor = Color.Red
            };

            lblManualCount = new Label
            {
                Text = "人工: 0",
                Location = new Point(340, 25),
                Size = new Size(100, 20),
                ForeColor = Color.FromArgb(255, 193, 7)
            };

            grpStatistics.Controls.AddRange(new Control[] {
                lblTotalCount, lblSuccessCount, lblFailedCount, lblManualCount
            });

            // 文件列表区域
            grpFileList = new GroupBox
            {
                Text = "文件列表",
                Location = new Point(10, 230),
                Size = new Size(960, 400),
                Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            lstFiles = new ListView
            {
                Location = new Point(10, 25),
                Size = new Size(940, 320),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            lstFiles.Columns.Add("文件名", 180);
            lstFiles.Columns.Add("扫描路径", 200);
            lstFiles.Columns.Add("输出路径", 200);
            lstFiles.Columns.Add("大小", 70);
            lstFiles.Columns.Add("类型", 50);
            lstFiles.Columns.Add("状态", 80);
            lstFiles.Columns.Add("条形码", 130);
            lstFiles.Columns.Add("识别时间", 130);

            btnManualReview = new Button
            {
                Text = "人工审核",
                Location = new Point(10, 355),
                Size = new Size(100, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            // 添加双击预览事件
            lstFiles.DoubleClick += (s, e) =>
            {
                if (lstFiles.SelectedItems.Count > 0)
                {
                    var item = lstFiles.SelectedItems[0];
                    var fileItem = item.Tag as FileItem;
                    if (fileItem != null)
                    {
                        // 优先打开输出路径，其次打开原始路径
                        string filePath = fileItem.OutputFilePath ?? fileItem.FilePath;
                        if (System.IO.File.Exists(filePath))
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = filePath,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        else
                        {
                            MessageBox.Show("文件不存在", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            };

            grpFileList.Controls.AddRange(new Control[] {
                lstFiles, btnManualReview
            });

            // 状态栏
            statusStrip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel
            {
                Text = "就绪"
            };
            statusStrip.Items.Add(lblStatus);

            // 添加所有控件到窗体
            this.Controls.AddRange(new Control[] {
                grpConfig, grpControl, grpStatistics, grpFileList, statusStrip
            });

            // 初始化定时器
            scanTimer = new System.Windows.Forms.Timer();
        }

        private void LoadConfigToUI()
        {
            txtScanFolder.Text = config.ScanFolder;
            txtOutputFolder.Text = config.OutputFolder;
            numInterval.Value = config.ScanInterval / 1000;
        }

        private void SetupEventHandlers()
        {
            btnBrowseScan.Click += (s, e) =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "选择扫描文件夹";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        txtScanFolder.Text = dialog.SelectedPath;
                        config.ScanFolder = dialog.SelectedPath;
                        config.Save();
                    }
                }
            };

            btnBrowseOutput.Click += (s, e) =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "选择输出文件夹";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        txtOutputFolder.Text = dialog.SelectedPath;
                        config.OutputFolder = dialog.SelectedPath;
                        config.Save();
                    }
                }
            };

            numInterval.ValueChanged += (s, e) =>
            {
                config.ScanInterval = (int)numInterval.Value * 1000;
                config.Save();
                if (scanTimer.Enabled)
                {
                    scanTimer.Interval = config.ScanInterval;
                }
            };

            btnStartScan.Click += (s, e) =>
            {
                StartAutoScan();
            };

            btnStopScan.Click += (s, e) =>
            {
                StopAutoScan();
            };

            btnScanOnce.Click += (s, e) =>
            {
                PerformScan();
            };

            btnRefresh.Click += (s, e) =>
            {
                RefreshFileList();
            };

            btnManualReview.Click += (s, e) =>
            {
                if (lstFiles.SelectedItems.Count > 0)
                {
                    var item = lstFiles.SelectedItems[0];
                    var fileItem = item.Tag as FileItem;
                    if (fileItem != null)
                    {
                        using (var reviewForm = new ManualReviewForm(fileItem, (fi, barcode) =>
                        {
                            // 根据文件状态选择不同的处理方式
                            if (fi.Status == RecognitionStatus.Success)
                            {
                                // 已识别成功的文件，只重命名
                                scanService.ManualRenameFile(fi, barcode);
                            }
                            else
                            {
                                // 识别失败或待识别的文件，需要移动
                                scanService.ManualSetBarcode(fi, barcode);
                                if (fi.Status == RecognitionStatus.Failed)
                                {
                                    totalStats.FailedCount--;
                                }
                                totalStats.ManualCount++;
                            }
                            UpdateFileListItem(item, fi);
                            UpdateStatistics();
                        }))
                        {
                            reviewForm.ShowDialog();
                        }
                    }
                }
            };

            scanService.FileProcessed += (s, fileItem) =>
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => AddFileToList(fileItem)));
                }
                else
                {
                    AddFileToList(fileItem);
                }
            };

            scanService.FileRecognized += (s, fileItem) =>
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        UpdateFileItemInList(fileItem);
                        UpdateStatisticsAfterRecognition(fileItem);
                    }));
                }
                else
                {
                    UpdateFileItemInList(fileItem);
                    UpdateStatisticsAfterRecognition(fileItem);
                }
            };

            scanService.StatisticsUpdated += (s, stats) =>
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        totalStats.Add(stats);
                        UpdateStatistics();
                    }));
                }
                else
                {
                    totalStats.Add(stats);
                    UpdateStatistics();
                }
            };

            scanTimer.Tick += (s, e) =>
            {
                PerformScan();
            };
        }

        private void StartAutoScan()
        {
            if (string.IsNullOrEmpty(config.ScanFolder))
            {
                MessageBox.Show("请先设置扫描文件夹", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(config.OutputFolder))
            {
                MessageBox.Show("请先设置输出文件夹", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            config.AutoScan = true;
            config.Save();

            scanTimer.Interval = config.ScanInterval;
            scanTimer.Start();

            btnStartScan.Enabled = false;
            btnStopScan.Enabled = true;
            btnScanOnce.Enabled = false;

            lblStatus.Text = "自动扫描中...";
        }

        private void StopAutoScan()
        {
            config.AutoScan = false;
            config.Save();

            scanTimer.Stop();
            scanService.StopRecognition(); // 停止识别

            btnStartScan.Enabled = true;
            btnStopScan.Enabled = false;
            btnScanOnce.Enabled = true;

            lblStatus.Text = "已停止扫描";
        }

        private void PerformScan()
        {
            lblStatus.Text = $"正在扫描... {DateTime.Now:HH:mm:ss}";
            scanService.ScanFolder();
            lblStatus.Text = $"扫描完成 {DateTime.Now:HH:mm:ss}";
        }

        private void AddFileToList(FileItem fileItem)
        {
            var item = new ListViewItem(fileItem.FileName);
            // 扫描路径：优先显示原始路径，如果没有则显示当前路径
            item.SubItems.Add(fileItem.OriginalFilePath ?? fileItem.FilePath);
            item.SubItems.Add(fileItem.OutputFilePath ?? ""); // 输出路径
            item.SubItems.Add(fileItem.FormattedSize);
            item.SubItems.Add(fileItem.FileType);
            item.SubItems.Add(fileItem.StatusDescription);
            item.SubItems.Add(fileItem.BarcodeContent ?? "");
            // 识别时间：如果是最小值则显示空
            item.SubItems.Add(fileItem.RecognitionTime != DateTime.MinValue ?
                fileItem.RecognitionTime.ToString("yyyy-MM-dd HH:mm:ss") : "");
            item.Tag = fileItem;

            // 根据状态设置颜色
            if (fileItem.Status == RecognitionStatus.Success)
            {
                item.BackColor = Color.LightGreen;
            }
            else if (fileItem.Status == RecognitionStatus.Failed)
            {
                item.BackColor = Color.LightPink;
            }
            else if (fileItem.Status == RecognitionStatus.Manual)
            {
                item.BackColor = Color.LightYellow;
            }
            else if (fileItem.Status == RecognitionStatus.Recognizing)
            {
                item.BackColor = Color.LightBlue;
            }

            lstFiles.Items.Insert(0, item);
        }

        private void UpdateFileListItem(ListViewItem item, FileItem fileItem)
        {
            item.SubItems[0].Text = fileItem.FileName;
            // 扫描路径：优先显示原始路径，如果没有则显示当前路径
            item.SubItems[1].Text = fileItem.OriginalFilePath ?? fileItem.FilePath;
            item.SubItems[2].Text = fileItem.OutputFilePath ?? ""; // 输出路径
            item.SubItems[3].Text = fileItem.FormattedSize;
            item.SubItems[4].Text = fileItem.FileType;
            item.SubItems[5].Text = fileItem.StatusDescription;
            item.SubItems[6].Text = fileItem.BarcodeContent ?? "";
            // 识别时间：如果是最小值则显示空
            item.SubItems[7].Text = fileItem.RecognitionTime != DateTime.MinValue ?
                fileItem.RecognitionTime.ToString("yyyy-MM-dd HH:mm:ss") : "";

            if (fileItem.Status == RecognitionStatus.Success)
            {
                item.BackColor = Color.LightGreen;
            }
            else if (fileItem.Status == RecognitionStatus.Manual)
            {
                item.BackColor = Color.LightYellow;
            }
            else if (fileItem.Status == RecognitionStatus.Failed)
            {
                item.BackColor = Color.LightPink;
            }
            else if (fileItem.Status == RecognitionStatus.Recognizing)
            {
                item.BackColor = Color.LightBlue;
            }
            else if (fileItem.Status == RecognitionStatus.Pending)
            {
                item.BackColor = Color.White;
            }
        }

        private void UpdateStatistics()
        {
            lblTotalCount.Text = $"扫描总数: {totalStats.TotalCount}";
            lblSuccessCount.Text = $"成功: {totalStats.SuccessCount}";
            lblFailedCount.Text = $"失败: {totalStats.FailedCount}";
            lblManualCount.Text = $"人工: {totalStats.ManualCount}";
        }

        /// <summary>
        /// 更新列表中的文件项（异步识别后）
        /// </summary>
        private void UpdateFileItemInList(FileItem fileItem)
        {
            // 暂停绘制以避免闪烁
            lstFiles.BeginUpdate();
            try
            {
                // 查找对应的 ListViewItem
                foreach (ListViewItem item in lstFiles.Items)
                {
                    if (item.Tag is FileItem itemFile)
                    {
                        // 使用OriginalFilePath进行匹配，如果为空则使用FilePath
                        string itemPath = itemFile.OriginalFilePath ?? itemFile.FilePath;
                        string targetPath = fileItem.OriginalFilePath ?? fileItem.FilePath;
                        
                        if (itemPath == targetPath)
                        {
                            UpdateFileListItem(item, fileItem);
                            // 更新Tag为最新的fileItem
                            item.Tag = fileItem;
                            break;
                        }
                    }
                }
            }
            finally
            {
                lstFiles.EndUpdate();
            }
        }

        /// <summary>
        /// 识别完成后更新统计
        /// </summary>
        private void UpdateStatisticsAfterRecognition(FileItem fileItem)
        {
            // 减少 Pending 计数
            if (totalStats.PendingCount > 0)
            {
                totalStats.PendingCount--;
            }

            // 增加对应状态的计数
            if (fileItem.Status == RecognitionStatus.Success)
            {
                totalStats.SuccessCount++;
            }
            else if (fileItem.Status == RecognitionStatus.Failed)
            {
                totalStats.FailedCount++;
            }
            else if (fileItem.Status == RecognitionStatus.Manual)
            {
                totalStats.ManualCount++;
            }

            UpdateStatistics();
        }
       

        /// <summary>
        /// 刷新文件列表显示
        /// </summary>
        private void RefreshFileList()
        {
            // 暂停绘制以提高性能
            lstFiles.BeginUpdate();

            try
            {
                // 更新所有项目的显示
                foreach (ListViewItem item in lstFiles.Items)
                {
                    if (item.Tag is FileItem fileItem)
                    {
                        UpdateFileListItem(item, fileItem);
                    }
                }
            }
            finally
            {
                // 恢复绘制
                lstFiles.EndUpdate();
            }
        }
        
        /// <summary>
        /// 初始化系统托盘图标
        /// </summary>
        private void InitializeTrayIcon()
        {
            // 创建托盘图标
            notifyIcon = new NotifyIcon();
            notifyIcon.Text = "条形码识别工具";
            
            // 尝试加载图标（按优先级）
            try
            {
                // 方案1：使用窗体图标（如果已设置）
                if (this.Icon != null)
                {
                    notifyIcon.Icon = this.Icon;
                }
                // 方案2：使用系统信息图标（更明显的图标）
                else
                {
                    notifyIcon.Icon = SystemIcons.Information;
                }
            }
            catch
            {
                // 方案3：使用系统应用程序图标作为后备
                notifyIcon.Icon = SystemIcons.Application;
            }
            
            notifyIcon.Visible = true;
            
            // 创建右键菜单
            trayContextMenu = new ContextMenuStrip();
            
            // 添加菜单项
            var showMenuItem = new ToolStripMenuItem("显示主界面");
            showMenuItem.Click += (s, e) => ShowMainWindow();
            trayContextMenu.Items.Add(showMenuItem);
            
            trayContextMenu.Items.Add(new ToolStripSeparator()); // 分隔线
            
            var exitMenuItem = new ToolStripMenuItem("退出程序");
            exitMenuItem.Click += (s, e) => ExitApplication();
            trayContextMenu.Items.Add(exitMenuItem);
            
            // 设置右键菜单
            notifyIcon.ContextMenuStrip = trayContextMenu;
            
            // 双击托盘图标显示主窗口
            notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }
        
        /// <summary>
        /// 显示主窗口
        /// </summary>
        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }
        
        /// <summary>
        /// 退出应用程序
        /// </summary>
        private void ExitApplication()
        {
            // 停止扫描
            if (scanTimer.Enabled)
            {
                scanTimer.Stop();
                scanService.StopRecognition();
            }
            
            // 保存配置
            config.Save();
            
            // 移除托盘图标
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            
            // 退出应用
            Application.Exit();
        }
        
        /// <summary>
        /// 重写窗体关闭事件，最小化到托盘而非关闭
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 如果不是真正退出，则最小化到托盘
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // 取消关闭
                this.Hide(); // 隐藏窗口
                notifyIcon.ShowBalloonTip(2000, "条形码识别工具", "程序已最小化到系统托盘，继续后台运行", ToolTipIcon.Info);
            }
            else
            {
                // 真正退出时，清理托盘图标
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }
                base.OnFormClosing(e);
            }
        }
    }
}

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
                Size = new Size(500, 120),
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

            grpControl.Controls.AddRange(new Control[] {
                btnStartScan, btnStopScan, btnScanOnce
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

            lstFiles.Columns.Add("文件名", 200);
            lstFiles.Columns.Add("文件路径", 250);
            lstFiles.Columns.Add("大小", 80);
            lstFiles.Columns.Add("类型", 60);
            lstFiles.Columns.Add("状态", 80);
            lstFiles.Columns.Add("条形码", 150);
            lstFiles.Columns.Add("识别时间", 140);

            btnManualReview = new Button
            {
                Text = "人工审核",
                Location = new Point(10, 355),
                Size = new Size(100, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
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

            btnManualReview.Click += (s, e) =>
            {
                if (lstFiles.SelectedItems.Count > 0)
                {
                    var item = lstFiles.SelectedItems[0];
                    var fileItem = item.Tag as FileItem;
                    if (fileItem != null && (fileItem.Status == RecognitionStatus.Failed || fileItem.Status == RecognitionStatus.Pending))
                    {
                        using (var reviewForm = new ManualReviewForm(fileItem, (fi, barcode) =>
                        {
                            scanService.ManualSetBarcode(fi, barcode);
                            UpdateFileListItem(item, fi);
                            totalStats.ManualCount++;
                            totalStats.FailedCount--;
                            UpdateStatistics();
                        }))
                        {
                            reviewForm.ShowDialog();
                        }
                    }
                    else
                    {
                        MessageBox.Show("只能对识别失败或待识别的文件进行人工审核", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            item.SubItems.Add(fileItem.FilePath);
            item.SubItems.Add(fileItem.FormattedSize);
            item.SubItems.Add(fileItem.FileType);
            item.SubItems.Add(fileItem.StatusDescription);
            item.SubItems.Add(fileItem.BarcodeContent ?? "");
            item.SubItems.Add(fileItem.RecognitionTime.ToString("yyyy-MM-dd HH:mm:ss"));
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

            lstFiles.Items.Insert(0, item);
        }

        private void UpdateFileListItem(ListViewItem item, FileItem fileItem)
        {
            item.SubItems[4].Text = fileItem.StatusDescription;
            item.SubItems[5].Text = fileItem.BarcodeContent ?? "";
            item.SubItems[6].Text = fileItem.RecognitionTime.ToString("yyyy-MM-dd HH:mm:ss");

            if (fileItem.Status == RecognitionStatus.Success)
            {
                item.BackColor = Color.LightGreen;
            }
            else if (fileItem.Status == RecognitionStatus.Manual)
            {
                item.BackColor = Color.LightYellow;
            }
        }

        private void UpdateStatistics()
        {
            lblTotalCount.Text = $"扫描总数: {totalStats.TotalCount}";
            lblSuccessCount.Text = $"成功: {totalStats.SuccessCount}";
            lblFailedCount.Text = $"失败: {totalStats.FailedCount}";
            lblManualCount.Text = $"人工: {totalStats.ManualCount}";
        }
    }
}

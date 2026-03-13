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

            btnRefresh = new Button
            {
                Text = "刷新列表",
                Location = new Point(460, 25),
                Size = new Size(100, 35),
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
            // 查找对应的 ListViewItem
            foreach (ListViewItem item in lstFiles.Items)
            {
                if (item.Tag is FileItem itemFile && itemFile.FilePath == fileItem.OriginalFilePath)
                {
                    UpdateFileListItem(item, fileItem);
                    break;
                }
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

            // 自动刷新列表（每识别完一个文件刷新一次）
            RefreshFileList();
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
    }
}

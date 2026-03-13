using System;
using System.Collections.Generic;
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
        
        // 排序状态
        private Dictionary<ListView, (int Column, bool Ascending)> sortStates = new Dictionary<ListView, (int, bool)>();

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
        private NumericUpDown numThreads;

        private GroupBox grpControl;
        private Button btnStartScan;
        private Button btnStopScan;
        private Button btnScanOnce;
        private Button btnRefresh;
        private Button btnClearList;

        private GroupBox grpStatistics;
        private Label lblTotalCount;
        private Label lblSuccessCount;
        private Label lblFailedCount;
        private Label lblManualCount;

        private GroupBox grpFileList;
        private TabControl tabFileFilter;
        private TabPage tabAll;
        private TabPage tabSuccess;
        private TabPage tabFailed;
        private TabPage tabManualReview;
        private TabPage tabSearchResult;
        private ListView lstFiles;
        private ListView lstSuccessFiles;
        private ListView lstFailedFiles;
        private ListView lstManualReviewFiles;
        private ListView lstSearchFiles;
        private Button btnManualReview;
        private TextBox txtSearch;
        private Button btnSearch;
        private Button btnClearSearch;
        
        // 文件列表数据源
        private List<FileItem> allFiles = new List<FileItem>();

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
                Size = new Size(80, 25),
                Minimum = 1,
                Maximum = 60,
                Value = config.ScanInterval / 1000
            };

            Label lblThreads = new Label
            {
                Text = "识别线程数:",
                Location = new Point(185, 85),
                Size = new Size(80, 20)
            };

            numThreads = new NumericUpDown
            {
                Location = new Point(270, 83),
                Size = new Size(80, 25),
                Minimum = 1,
                Maximum = 10,
                Value = config.RecognitionThreads
            };

            grpConfig.Controls.AddRange(new Control[] {
                lblScanFolder, txtScanFolder, btnBrowseScan,
                lblOutputFolder, txtOutputFolder, btnBrowseOutput,
                lblInterval, numInterval, lblThreads, numThreads
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
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btnStopScan = new Button
            {
                Text = "停止扫描",
                Location = new Point(135, 25),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };

            btnScanOnce = new Button
            {
                Text = "单次扫描",
                Location = new Point(260, 25),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btnRefresh = new Button
            {
                Text = "刷新列表",
                Location = new Point(385, 25),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btnClearList = new Button
            {
                Text = "清空列表",
                Location = new Point(385, 70),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(255, 193, 7),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };

            grpControl.Controls.AddRange(new Control[] {
                btnStartScan, btnStopScan, btnScanOnce, btnRefresh, btnClearList
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

            // 创建选项卡
            tabFileFilter = new TabControl
            {
                Location = new Point(5, 18),
                Size = new Size(950, 330),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Microsoft YaHei UI", 9F)
            };

            // 全部选项卡
            tabAll = new TabPage("全部 (0)");
            tabSuccess = new TabPage("成功 (0)");
            tabFailed = new TabPage("失败 (0)");
            tabManualReview = new TabPage("人工审核 (0)");
            tabSearchResult = new TabPage("搜索结果 (0)");

            // 创建五个ListView
            lstFiles = CreateListView();
            lstSuccessFiles = CreateListView();
            lstFailedFiles = CreateListView();
            lstManualReviewFiles = CreateListView();
            lstSearchFiles = CreateListView();

            tabAll.Controls.Add(lstFiles);
            tabSuccess.Controls.Add(lstSuccessFiles);
            tabFailed.Controls.Add(lstFailedFiles);
            tabManualReview.Controls.Add(lstManualReviewFiles);
            tabSearchResult.Controls.Add(lstSearchFiles);

            tabFileFilter.TabPages.Add(tabAll);
            tabFileFilter.TabPages.Add(tabSuccess);
            tabFileFilter.TabPages.Add(tabFailed);
            tabFileFilter.TabPages.Add(tabManualReview);
            tabFileFilter.TabPages.Add(tabSearchResult);

            // 双击预览事件
            lstFiles.DoubleClick += LstFiles_DoubleClick;
            lstSuccessFiles.DoubleClick += LstFiles_DoubleClick;
            lstFailedFiles.DoubleClick += LstFiles_DoubleClick;
            lstManualReviewFiles.DoubleClick += LstFiles_DoubleClick;
            lstSearchFiles.DoubleClick += LstFiles_DoubleClick;

            // 搜索框
            txtSearch = new TextBox
            {
                Location = new Point(5, 355),
                Size = new Size(150, 25),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            btnSearch = new Button
            {
                Text = "搜索",
                Location = new Point(160, 355),
                Size = new Size(60, 25),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            btnClearSearch = new Button
            {
                Text = "清除",
                Location = new Point(225, 355),
                Size = new Size(60, 25),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            btnManualReview = new Button
            {
                Text = "人工审核",
                Location = new Point(845, 355),
                Size = new Size(100, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            grpFileList.Controls.AddRange(new Control[] {
                tabFileFilter, txtSearch, btnSearch, btnClearSearch, btnManualReview
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

        /// <summary>
        /// 创建ListView控件（统一配置）
        /// </summary>
        private ListView CreateListView()
        {
            var lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            // 启用双缓冲以减少闪烁
            lv.DoubleBuffered(true);

            lv.Columns.Add("文件名", 180);
            lv.Columns.Add("扫描路径", 200);
            lv.Columns.Add("输出路径", 200);
            lv.Columns.Add("大小", 70);
            lv.Columns.Add("类型", 50);
            lv.Columns.Add("状态", 80);
            lv.Columns.Add("条形码", 130);
            lv.Columns.Add("识别时间", 130);

            // 添加列点击排序事件
            lv.ColumnClick += ListView_ColumnClick;

            return lv;
        }

        /// <summary>
        /// 列点击排序事件
        /// </summary>
        private void ListView_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            var lv = sender as ListView;
            if (lv == null) return;

            // 获取当前排序状态
            if (!sortStates.ContainsKey(lv))
            {
                sortStates[lv] = (e.Column, true);
            }
            else
            {
                var current = sortStates[lv];
                if (current.Column == e.Column)
                {
                    // 同一列，切换排序方向
                    sortStates[lv] = (e.Column, !current.Ascending);
                }
                else
                {
                    // 不同列，默认正序
                    sortStates[lv] = (e.Column, true);
                }
            }

            // 执行排序
            var sortInfo = sortStates[lv];
            lv.ListViewItemSorter = new ListViewItemComparer(e.Column, sortInfo.Ascending);
            lv.Sort();
        }

        /// <summary>
        /// 双击预览文件
        /// </summary>
        private void LstFiles_DoubleClick(object sender, EventArgs e)
        {
            var lv = sender as ListView;
            if (lv != null && lv.SelectedItems.Count > 0)
            {
                var item = lv.SelectedItems[0];
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
        }

        /// <summary>
        /// 获取当前选中的ListView
        /// </summary>
        private ListView GetCurrentListView()
        {
            if (tabFileFilter.SelectedTab == tabAll)
                return lstFiles;
            else if (tabFileFilter.SelectedTab == tabSuccess)
                return lstSuccessFiles;
            else if (tabFileFilter.SelectedTab == tabFailed)
                return lstFailedFiles;
            else if (tabFileFilter.SelectedTab == tabManualReview)
                return lstManualReviewFiles;
            return lstFiles;
        }

        private void LoadConfigToUI()
        {
            txtScanFolder.Text = config.ScanFolder;
            txtOutputFolder.Text = config.OutputFolder;
            numInterval.Value = config.ScanInterval / 1000;
            numThreads.Value = config.RecognitionThreads;
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
                        scanService.UpdateCropOutputFolder();
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

            numThreads.ValueChanged += (s, e) =>
            {
                config.RecognitionThreads = (int)numThreads.Value;
                config.Save();
                scanService.UpdateRecognitionThreads(config.RecognitionThreads);
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

            btnClearList.Click += (s, e) =>
            {
                ClearAllFiles();
            };

            // 搜索功能
            btnSearch.Click += (s, e) =>
            {
                PerformSearch();
            };

            txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    PerformSearch();
                    e.Handled = true;
                }
            };

            btnClearSearch.Click += (s, e) =>
            {
                txtSearch.Text = "";
                lstSearchFiles.Items.Clear();
                tabSearchResult.Text = "搜索结果 (0)";
            };

            btnManualReview.Click += (s, e) =>
            {
                var currentLv = GetCurrentListView();
                if (currentLv.SelectedItems.Count > 0)
                {
                    var item = currentLv.SelectedItems[0];
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
                            // 更新所有列表中的文件项
                            UpdateFileItemInAllLists(fi);
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

            // 执行一次扫描
            PerformScan();

            // 启动待识别文件的识别（处理之前已存在但未识别的文件）
            scanService.StartPendingRecognition();
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

        /// <summary>
        /// 清空所有文件列表
        /// </summary>
        private void ClearAllFiles()
        {
            var result = MessageBox.Show(
                "确定要清空文件列表吗？\n\n这将清除所有已扫描的文件记录，释放内存。\n已处理的文件不会被删除。",
                "确认清空",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes)
            {
                // 停止扫描和识别
                if (scanTimer.Enabled)
                {
                    scanTimer.Stop();
                }
                scanService.StopRecognition();

                // 清空服务层的数据
                scanService.ClearAllFiles();

                // 清空所有列表
                lstFiles.Items.Clear();
                lstSuccessFiles.Items.Clear();
                lstFailedFiles.Items.Clear();
                lstManualReviewFiles.Items.Clear();
                lstSearchFiles.Items.Clear();
                allFiles.Clear();

                // 重置统计
                totalStats = new ScanStatistics();
                UpdateStatistics();
                UpdateTabTitles();

                lblStatus.Text = "列表已清空";

                // 恢复按钮状态
                btnStartScan.Enabled = true;
                btnStopScan.Enabled = false;
                btnScanOnce.Enabled = true;
            }
        }

        /// <summary>
        /// 创建ListViewItem
        /// </summary>
        private ListViewItem CreateListViewItem(FileItem fileItem)
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

            return item;
        }

        /// <summary>
        /// 添加文件到列表（同时更新多个列表和选项卡标题）
        /// </summary>
        private void AddFileToList(FileItem fileItem)
        {
            // 添加到全部列表
            var item = CreateListViewItem(fileItem);
            lstFiles.Items.Insert(0, item);

            // 根据状态添加到对应列表
            if (fileItem.Status == RecognitionStatus.Success)
            {
                var successItem = CreateListViewItem(fileItem);
                lstSuccessFiles.Items.Insert(0, successItem);
            }
            else if (fileItem.Status == RecognitionStatus.Failed)
            {
                var failedItem = CreateListViewItem(fileItem);
                lstFailedFiles.Items.Insert(0, failedItem);
            }
            else if (fileItem.Status == RecognitionStatus.Manual)
            {
                var manualItem = CreateListViewItem(fileItem);
                lstManualReviewFiles.Items.Insert(0, manualItem);
            }

            // 保存到数据源
            allFiles.Add(fileItem);

            // 更新选项卡标题
            UpdateTabTitles();
        }

        /// <summary>
        /// 更新ListViewItem内容（无闪烁更新）
        /// </summary>
        private void UpdateFileListItem(ListViewItem item, FileItem fileItem)
        {
            // 直接更新SubItems的文本，避免重建项目
            item.SubItems[0].Text = fileItem.FileName;
            item.SubItems[1].Text = fileItem.OriginalFilePath ?? fileItem.FilePath;
            item.SubItems[2].Text = fileItem.OutputFilePath ?? "";
            item.SubItems[3].Text = fileItem.FormattedSize;
            item.SubItems[4].Text = fileItem.FileType;
            item.SubItems[5].Text = fileItem.StatusDescription;
            item.SubItems[6].Text = fileItem.BarcodeContent ?? "";
            item.SubItems[7].Text = fileItem.RecognitionTime != DateTime.MinValue ?
                fileItem.RecognitionTime.ToString("yyyy-MM-dd HH:mm:ss") : "";

            // 更新背景颜色
            Color newColor = fileItem.Status switch
            {
                RecognitionStatus.Success => Color.LightGreen,
                RecognitionStatus.Manual => Color.LightYellow,
                RecognitionStatus.Failed => Color.LightPink,
                RecognitionStatus.Recognizing => Color.LightBlue,
                _ => Color.White
            };
            
            if (item.BackColor != newColor)
            {
                item.BackColor = newColor;
            }
        }

        /// <summary>
        /// 更新选项卡标题
        /// </summary>
        private void UpdateTabTitles()
        {
            tabAll.Text = $"全部 ({lstFiles.Items.Count})";
            tabSuccess.Text = $"成功 ({lstSuccessFiles.Items.Count})";
            tabFailed.Text = $"失败 ({lstFailedFiles.Items.Count})";
            tabManualReview.Text = $"人工审核 ({lstManualReviewFiles.Items.Count})";
        }

        /// <summary>
        /// 执行搜索
        /// </summary>
        private void PerformSearch()
        {
            string keyword = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("请输入搜索关键字", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            lstSearchFiles.Items.Clear();
            int count = 0;

            foreach (var fileItem in allFiles)
            {
                // 模糊匹配文件名或条形码内容
                if (fileItem.FileName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (fileItem.BarcodeContent != null && fileItem.BarcodeContent.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var item = CreateListViewItem(fileItem);
                    lstSearchFiles.Items.Add(item);
                    count++;
                }
            }

            tabSearchResult.Text = $"搜索结果 ({count})";
            tabFileFilter.SelectedTab = tabSearchResult;

            if (count == 0)
            {
                MessageBox.Show($"未找到包含 \"{keyword}\" 的文件", "搜索结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        /// 更新所有列表中的文件项（异步识别后）
        /// </summary>
        private void UpdateFileItemInList(FileItem fileItem)
        {
            // 暂停绘制以避免闪烁
            lstFiles.BeginUpdate();
            lstSuccessFiles.BeginUpdate();
            lstFailedFiles.BeginUpdate();
            lstManualReviewFiles.BeginUpdate();
            try
            {
                // 在全部列表中查找并更新
                bool foundInSuccess = false;
                bool foundInFailed = false;
                bool foundInManual = false;

                foreach (ListViewItem item in lstFiles.Items)
                {
                    if (item.Tag is FileItem itemFile)
                    {
                        string itemPath = itemFile.OriginalFilePath ?? itemFile.FilePath;
                        string targetPath = fileItem.OriginalFilePath ?? fileItem.FilePath;
                        
                        if (itemPath == targetPath)
                        {
                            UpdateFileListItem(item, fileItem);
                            item.Tag = fileItem;
                            break;
                        }
                    }
                }

                // 在成功列表中查找
                foreach (ListViewItem item in lstSuccessFiles.Items)
                {
                    if (item.Tag is FileItem itemFile)
                    {
                        string itemPath = itemFile.OriginalFilePath ?? itemFile.FilePath;
                        string targetPath = fileItem.OriginalFilePath ?? fileItem.FilePath;
                        
                        if (itemPath == targetPath)
                        {
                            foundInSuccess = true;
                            // 如果状态变成失败或人工，需要从成功列表移除
                            if (fileItem.Status == RecognitionStatus.Failed)
                            {
                                lstSuccessFiles.Items.Remove(item);
                                var failedItem = CreateListViewItem(fileItem);
                                lstFailedFiles.Items.Insert(0, failedItem);
                            }
                            else if (fileItem.Status == RecognitionStatus.Manual)
                            {
                                lstSuccessFiles.Items.Remove(item);
                                var manualItem = CreateListViewItem(fileItem);
                                lstManualReviewFiles.Items.Insert(0, manualItem);
                            }
                            else
                            {
                                UpdateFileListItem(item, fileItem);
                                item.Tag = fileItem;
                            }
                            break;
                        }
                    }
                }

                // 在失败列表中查找
                foreach (ListViewItem item in lstFailedFiles.Items)
                {
                    if (item.Tag is FileItem itemFile)
                    {
                        string itemPath = itemFile.OriginalFilePath ?? itemFile.FilePath;
                        string targetPath = fileItem.OriginalFilePath ?? fileItem.FilePath;
                        
                        if (itemPath == targetPath)
                        {
                            foundInFailed = true;
                            // 如果状态变成成功或人工，需要从失败列表移除
                            if (fileItem.Status == RecognitionStatus.Success)
                            {
                                lstFailedFiles.Items.Remove(item);
                                var successItem = CreateListViewItem(fileItem);
                                lstSuccessFiles.Items.Insert(0, successItem);
                            }
                            else if (fileItem.Status == RecognitionStatus.Manual)
                            {
                                lstFailedFiles.Items.Remove(item);
                                var manualItem = CreateListViewItem(fileItem);
                                lstManualReviewFiles.Items.Insert(0, manualItem);
                            }
                            else
                            {
                                UpdateFileListItem(item, fileItem);
                                item.Tag = fileItem;
                            }
                            break;
                        }
                    }
                }

                // 在人工审核列表中查找
                foreach (ListViewItem item in lstManualReviewFiles.Items)
                {
                    if (item.Tag is FileItem itemFile)
                    {
                        string itemPath = itemFile.OriginalFilePath ?? itemFile.FilePath;
                        string targetPath = fileItem.OriginalFilePath ?? fileItem.FilePath;
                        
                        if (itemPath == targetPath)
                        {
                            foundInManual = true;
                            // 如果状态变成成功或失败，需要从人工列表移除
                            if (fileItem.Status == RecognitionStatus.Success)
                            {
                                lstManualReviewFiles.Items.Remove(item);
                                var successItem = CreateListViewItem(fileItem);
                                lstSuccessFiles.Items.Insert(0, successItem);
                            }
                            else if (fileItem.Status == RecognitionStatus.Failed)
                            {
                                lstManualReviewFiles.Items.Remove(item);
                                var failedItem = CreateListViewItem(fileItem);
                                lstFailedFiles.Items.Insert(0, failedItem);
                            }
                            else
                            {
                                UpdateFileListItem(item, fileItem);
                                item.Tag = fileItem;
                            }
                            break;
                        }
                    }
                }

                // 如果之前不在任何列表，现在需要添加
                if (!foundInSuccess && !foundInFailed && !foundInManual)
                {
                    if (fileItem.Status == RecognitionStatus.Success)
                    {
                        var successItem = CreateListViewItem(fileItem);
                        lstSuccessFiles.Items.Insert(0, successItem);
                    }
                    else if (fileItem.Status == RecognitionStatus.Failed)
                    {
                        var failedItem = CreateListViewItem(fileItem);
                        lstFailedFiles.Items.Insert(0, failedItem);
                    }
                    else if (fileItem.Status == RecognitionStatus.Manual)
                    {
                        var manualItem = CreateListViewItem(fileItem);
                        lstManualReviewFiles.Items.Insert(0, manualItem);
                    }
                }

                // 更新选项卡标题
                UpdateTabTitles();
            }
            finally
            {
                lstFiles.EndUpdate();
                lstSuccessFiles.EndUpdate();
                lstFailedFiles.EndUpdate();
                lstManualReviewFiles.EndUpdate();
            }
        }

        /// <summary>
        /// 更新所有列表中的文件项（人工审核后）
        /// </summary>
        private void UpdateFileItemInAllLists(FileItem fileItem)
        {
            UpdateFileItemInList(fileItem);
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
            UpdateTabTitles();
        }
       

        /// <summary>
        /// 刷新文件列表显示
        /// </summary>
        private void RefreshFileList()
        {
            // 暂停绘制以提高性能
            lstFiles.BeginUpdate();
            lstSuccessFiles.BeginUpdate();
            lstFailedFiles.BeginUpdate();
            lstManualReviewFiles.BeginUpdate();

            try
            {
                // 更新全部列表
                foreach (ListViewItem item in lstFiles.Items)
                {
                    if (item.Tag is FileItem fileItem)
                    {
                        UpdateFileListItem(item, fileItem);
                    }
                }

                // 更新成功列表
                foreach (ListViewItem item in lstSuccessFiles.Items)
                {
                    if (item.Tag is FileItem fileItem)
                    {
                        UpdateFileListItem(item, fileItem);
                    }
                }

                // 更新失败列表
                foreach (ListViewItem item in lstFailedFiles.Items)
                {
                    if (item.Tag is FileItem fileItem)
                    {
                        UpdateFileListItem(item, fileItem);
                    }
                }

                // 更新人工审核列表
                foreach (ListViewItem item in lstManualReviewFiles.Items)
                {
                    if (item.Tag is FileItem fileItem)
                    {
                        UpdateFileListItem(item, fileItem);
                    }
                }

                // 更新选项卡标题
                UpdateTabTitles();
            }
            finally
            {
                // 恢复绘制
                lstFiles.EndUpdate();
                lstSuccessFiles.EndUpdate();
                lstFailedFiles.EndUpdate();
                lstManualReviewFiles.EndUpdate();
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

    /// <summary>
    /// ListViewItem 比较器（用于排序）
    /// </summary>
    public class ListViewItemComparer : System.Collections.IComparer
    {
        private readonly int column;
        private readonly bool ascending;

        public ListViewItemComparer(int column, bool ascending)
        {
            this.column = column;
            this.ascending = ascending;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not ListViewItem itemX || y is not ListViewItem itemY)
                return 0;

            // 获取比较值
            string textX = column < itemX.SubItems.Count ? itemX.SubItems[column].Text : "";
            string textY = column < itemY.SubItems.Count ? itemY.SubItems[column].Text : "";

            // 特殊处理：识别时间列按日期排序
            if (column == 7)
            {
                DateTime dateX, dateY;
                if (DateTime.TryParse(textX, out dateX) && DateTime.TryParse(textY, out dateY))
                {
                    return ascending ? dateX.CompareTo(dateY) : dateY.CompareTo(dateX);
                }
            }

            // 默认字符串比较
            int result = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
            return ascending ? result : -result;
        }
    }

    /// <summary>
    /// ListView 双缓冲扩展
    /// </summary>
    public static class ListViewExtensions
    {
        public static void DoubleBuffered(this ListView lv, bool enabled)
        {
            var prop = typeof(Control).GetProperty("DoubleBuffered", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            prop?.SetValue(lv, enabled, null);
        }
    }
}

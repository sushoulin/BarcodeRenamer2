using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BarcodeRenamer2
{
    /// <summary>
    /// 文件扫描服务类
    /// </summary>
    public class FileScanService
    {
        private readonly AppConfig config;
        private readonly BarcodeRecognitionService recognitionService;
        private readonly HashSet<string> processedFiles;
        private readonly List<FileItem> pendingRecognitionQueue;
        private readonly object queueLock = new object();
        private readonly SemaphoreSlim recognitionSemaphore;
        private bool isRecognizing = false;
        private bool stopRequested = false;
        private CancellationTokenSource? recognitionCts;

        public event EventHandler<FileItem>? FileProcessed;
        public event EventHandler<FileItem>? FileRecognized;
        public event EventHandler<ScanStatistics>? StatisticsUpdated;

        public FileScanService(AppConfig config)
        {
            this.config = config;
            this.recognitionService = new BarcodeRecognitionService();
            this.processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            this.pendingRecognitionQueue = new List<FileItem>();
            this.recognitionSemaphore = new SemaphoreSlim(config.RecognitionThreads, config.RecognitionThreads);
            
            // 设置裁剪图片输出文件夹
            UpdateCropOutputFolder();
        }

        /// <summary>
        /// 更新识别线程数
        /// </summary>
        public void UpdateRecognitionThreads(int threads)
        {
            if (threads < 1) threads = 1;
            if (threads > 10) threads = 10;
            config.RecognitionThreads = threads;
        }

        /// <summary>
        /// 更新裁剪图片输出文件夹
        /// </summary>
        public void UpdateCropOutputFolder()
        {
            if (!string.IsNullOrEmpty(config.OutputFolder))
            {
                string cropFolder = Path.Combine(config.OutputFolder, "裁剪");
                recognitionService.SetCropOutputFolder(cropFolder);
            }
        }

        /// <summary>
        /// 清除已处理文件记录
        /// </summary>
        public void ClearProcessedFiles()
        {
            processedFiles.Clear();
        }

        /// <summary>
        /// 标记文件为已处理
        /// </summary>
        public void MarkFileAsProcessed(string filePath)
        {
            processedFiles.Add(filePath);
        }

        /// <summary>
        /// 检查文件是否已处理
        /// </summary>
        public bool IsFileProcessed(string filePath)
        {
            return processedFiles.Contains(filePath);
        }

        /// <summary>
        /// 扫描文件夹中的图片文件（快速扫描，只获取文件列表）
        /// 支持递归扫描子文件夹
        /// </summary>
        public void ScanFolder()
        {
            if (string.IsNullOrEmpty(config.ScanFolder) || !Directory.Exists(config.ScanFolder))
            {
                return;
            }

            var stats = new ScanStatistics();

            foreach (var format in config.SupportedFormats)
            {
                try
                {
                    // 使用 SearchOption.AllDirectories 递归扫描子文件夹
                    var files = Directory.GetFiles(config.ScanFolder, format, SearchOption.AllDirectories);
                    foreach (var filePath in files)
                    {
                        try
                        {
                            // 跳过已处理的文件
                            if (IsFileProcessed(filePath))
                            {
                                continue;
                            }

                            // 快速创建文件项，不进行识别
                            var fileItem = CreateFileItem(filePath);

                            // 标记文件为已处理
                            MarkFileAsProcessed(filePath);

                            stats.TotalCount++;
                            stats.PendingCount++;

                            // 立即通知UI显示文件
                            FileProcessed?.Invoke(this, fileItem);

                            // 添加到待识别队列
                            lock (queueLock)
                            {
                                pendingRecognitionQueue.Add(fileItem);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"扫描文件失败 {filePath}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"扫描格式 {format} 失败: {ex.Message}");
                }
            }

            StatisticsUpdated?.Invoke(this, stats);

            // 启动异步识别（如果队列有待识别文件且当前未在识别）
            StartRecognitionIfNeeded();
        }

        /// <summary>
        /// 启动待识别文件的识别（用于开始自动扫描时处理已存在的待识别文件）
        /// </summary>
        public void StartPendingRecognition()
        {
            StartRecognitionIfNeeded();
        }

        /// <summary>
        /// 如果需要则启动识别
        /// </summary>
        private void StartRecognitionIfNeeded()
        {
            int queueCount;
            lock (queueLock)
            {
                queueCount = pendingRecognitionQueue.Count;
            }

            if (queueCount > 0 && !isRecognizing)
            {
                _ = StartAsyncRecognition();
            }
        }

        /// <summary>
        /// 快速创建文件项，不进行识别
        /// </summary>
        private FileItem CreateFileItem(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            return new FileItem
            {
                FileName = fileInfo.Name,
                FilePath = filePath,
                FileSize = fileInfo.Length,
                FileType = fileInfo.Extension.ToUpper().TrimStart('.'),
                Status = RecognitionStatus.Pending,
                OriginalFilePath = filePath
            };
        }

        /// <summary>
        /// 停止识别
        /// </summary>
        public void StopRecognition()
        {
            stopRequested = true;
            recognitionCts?.Cancel();
            lock (queueLock)
            {
                pendingRecognitionQueue.Clear();
            }
        }

        /// <summary>
        /// 清空所有文件列表
        /// </summary>
        public void ClearAllFiles()
        {
            StopRecognition();
            processedFiles.Clear();
        }
        
        /// <summary>
        /// 启动异步识别（多线程）
        /// </summary>
        private async Task StartAsyncRecognition()
        {
            // 防止重复启动
            if (isRecognizing)
            {
                return;
            }
            
            isRecognizing = true;
            stopRequested = false;
            recognitionCts = new CancellationTokenSource();

            var tasks = new List<Task>();

            while (true)
            {
                if (stopRequested || recognitionCts.Token.IsCancellationRequested)
                {
                    break;
                }

                FileItem? fileItem = null;
                lock (queueLock)
                {
                    if (pendingRecognitionQueue.Count > 0)
                    {
                        fileItem = pendingRecognitionQueue[0];
                        pendingRecognitionQueue.RemoveAt(0);
                    }
                }

                if (fileItem == null)
                {
                    // 队列为空，等待一小段时间检查是否有新文件
                    await Task.Delay(100);
                    
                    lock (queueLock)
                    {
                        if (pendingRecognitionQueue.Count == 0)
                        {
                            break;
                        }
                    }
                    continue;
                }

                // 等待获取信号量（控制并发数）
                await recognitionSemaphore.WaitAsync();
                
                if (stopRequested || recognitionCts.Token.IsCancellationRequested)
                {
                    // 把文件放回队列
                    lock (queueLock)
                    {
                        pendingRecognitionQueue.Insert(0, fileItem);
                    }
                    recognitionSemaphore.Release();
                    break;
                }

                // 启动识别任务
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await RecognizeFileAsync(fileItem, recognitionCts.Token);
                    }
                    finally
                    {
                        recognitionSemaphore.Release();
                    }
                }, recognitionCts.Token);
                
                tasks.Add(task);
            }

            // 等待所有正在进行的识别任务完成
            await Task.WhenAll(tasks);

            isRecognizing = false;
            recognitionCts?.Dispose();
            recognitionCts = null;
            
            // 识别完成后，检查是否有新的待识别文件
            if (!stopRequested)
            {
                lock (queueLock)
                {
                    if (pendingRecognitionQueue.Count > 0)
                    {
                        _ = StartAsyncRecognition();
                    }
                }
            }
        }

        /// <summary>
        /// 异步识别单个文件
        /// </summary>
        private async Task RecognizeFileAsync(FileItem fileItem, CancellationToken cancellationToken)
        {
            try
            {
                // 检查是否请求停止
                if (stopRequested || cancellationToken.IsCancellationRequested)
                {
                    fileItem.Status = RecognitionStatus.Pending;
                    FileRecognized?.Invoke(this, fileItem);
                    return;
                }
                
                // 更新状态为识别中
                fileItem.Status = RecognitionStatus.Recognizing;
                FileRecognized?.Invoke(this, fileItem);

                // 检查文件是否被占用
                if (IsFileLocked(fileItem.FilePath))
                {
                    fileItem.Status = RecognitionStatus.Pending;
                    FileRecognized?.Invoke(this, fileItem);
                    return;
                }

                // 异步识别条形码
                var result = await Task.Run(() => recognitionService.Recognize(fileItem.FilePath), cancellationToken);
                fileItem.RecognitionTime = DateTime.Now;

                if (result.Success && !string.IsNullOrEmpty(result.Content))
                {
                    fileItem.Status = RecognitionStatus.Success;
                    fileItem.BarcodeContent = result.Content;

                    // 重命名并移动文件
                    MoveFile(fileItem);
                }
                else
                {
                    // 所有识别失败的情况都归类为 Failed
                    fileItem.Status = RecognitionStatus.Failed;
                }
            }
            catch (OperationCanceledException)
            {
                fileItem.Status = RecognitionStatus.Pending;
            }
            catch (Exception ex)
            {
                // 记录错误并设置为失败状态
                System.Diagnostics.Debug.WriteLine($"识别文件异常 {fileItem.FilePath}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈: {ex.StackTrace}");
                fileItem.Status = RecognitionStatus.Failed;
                fileItem.RecognitionTime = DateTime.Now;
            }
            finally
            {
                // 无论如何都通知UI更新
                FileRecognized?.Invoke(this, fileItem);
            }
        }

        /// <summary>
        /// 检查文件是否被锁定
        /// </summary>
        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        /// <summary>
        /// 重命名并移动文件
        /// </summary>
        private void MoveFile(FileItem fileItem)
        {
            if (string.IsNullOrEmpty(config.OutputFolder) || string.IsNullOrEmpty(fileItem.BarcodeContent))
            {
                return;
            }

            try
            {
                // 确保输出目录存在
                if (!Directory.Exists(config.OutputFolder))
                {
                    Directory.CreateDirectory(config.OutputFolder);
                }

                // 生成新文件名
                string extension = Path.GetExtension(fileItem.FilePath);
                string newFileName = $"{fileItem.BarcodeContent}{extension}";
                string newFilePath = Path.Combine(config.OutputFolder, newFileName);

                // 如果目标文件已存在，添加序号
                int counter = 1;
                while (File.Exists(newFilePath))
                {
                    newFileName = $"{fileItem.BarcodeContent}_{counter}{extension}";
                    newFilePath = Path.Combine(config.OutputFolder, newFileName);
                    counter++;
                }

                // 保存原始文件路径（用于人工审核）
                fileItem.OriginalFilePath = fileItem.FilePath;

                // 移动文件
                File.Move(fileItem.FilePath, newFilePath);

                // 更新文件信息
                fileItem.OutputFilePath = newFilePath;
                fileItem.FilePath = newFilePath;
                fileItem.FileName = newFileName;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"移动文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 手动设置条形码并移动文件
        /// </summary>
        public void ManualSetBarcode(FileItem fileItem, string barcode)
        {
            fileItem.BarcodeContent = barcode;
            fileItem.Status = RecognitionStatus.Manual;
            fileItem.IsManualReview = true;
            fileItem.RecognitionTime = DateTime.Now;

            MoveFile(fileItem);
        }

        /// <summary>
        /// 手动重命名已识别成功的文件
        /// </summary>
        public void ManualRenameFile(FileItem fileItem, string newBarcode)
        {
            if (string.IsNullOrEmpty(fileItem.OutputFilePath) || !File.Exists(fileItem.OutputFilePath))
            {
                return;
            }

            try
            {
                // 确保输出目录存在
                if (!Directory.Exists(config.OutputFolder))
                {
                    Directory.CreateDirectory(config.OutputFolder);
                }

                // 生成新文件名
                string extension = Path.GetExtension(fileItem.OutputFilePath);
                string newFileName = $"{newBarcode}{extension}";
                string newFilePath = Path.Combine(config.OutputFolder, newFileName);

                // 如果目标文件已存在，添加序号
                int counter = 1;
                while (File.Exists(newFilePath) && newFilePath != fileItem.OutputFilePath)
                {
                    newFileName = $"{newBarcode}_{counter}{extension}";
                    newFilePath = Path.Combine(config.OutputFolder, newFileName);
                    counter++;
                }

                // 如果新路径与当前路径相同，无需重命名
                if (newFilePath == fileItem.OutputFilePath)
                {
                    return;
                }

                // 重命名文件
                File.Move(fileItem.OutputFilePath, newFilePath);

                // 更新文件信息
                fileItem.BarcodeContent = newBarcode;
                fileItem.FileName = newFileName;
                fileItem.FilePath = newFilePath;
                fileItem.OutputFilePath = newFilePath;
                fileItem.IsManualReview = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"重命名文件失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 扫描统计类
    /// </summary>
    public class ScanStatistics
    {
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int ManualCount { get; set; }
        public int PendingCount { get; set; }

        public void Add(ScanStatistics other)
        {
            TotalCount += other.TotalCount;
            SuccessCount += other.SuccessCount;
            FailedCount += other.FailedCount;
            ManualCount += other.ManualCount;
            PendingCount += other.PendingCount;
        }
    }
}

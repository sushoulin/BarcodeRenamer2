using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly Queue<FileItem> pendingRecognitionQueue;
        private bool isRecognizing = false;
        private bool stopRequested = false;

        public event EventHandler<FileItem>? FileProcessed;
        public event EventHandler<FileItem>? FileRecognized;
        public event EventHandler<ScanStatistics>? StatisticsUpdated;

        public FileScanService(AppConfig config)
        {
            this.config = config;
            this.recognitionService = new BarcodeRecognitionService();
            this.processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            this.pendingRecognitionQueue = new Queue<FileItem>();
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
                    var files = Directory.GetFiles(config.ScanFolder, format);
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
                            pendingRecognitionQueue.Enqueue(fileItem);
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
            if (pendingRecognitionQueue.Count > 0)
            {
                if (!isRecognizing)
                {
                    _ = StartAsyncRecognition();
                }
                // 如果已经在识别中，新添加的文件会在队列中等待
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
            pendingRecognitionQueue.Clear();
        }
        
        /// <summary>
        /// 启动异步识别
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

            while (pendingRecognitionQueue.Count > 0 && !stopRequested)
            {
                var fileItem = pendingRecognitionQueue.Dequeue();

                try
                {
                    // 异步识别
                    await Task.Run(() => RecognizeFile(fileItem));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"识别文件失败 {fileItem.FilePath}: {ex.Message}");
                }
            }

            isRecognizing = false;
            
            // 识别完成后，检查是否有新的待识别文件
            if (pendingRecognitionQueue.Count > 0 && !stopRequested)
            {
                _ = StartAsyncRecognition();
            }
        }

        /// <summary>
        /// 识别单个文件
        /// </summary>
        private void RecognizeFile(FileItem fileItem)
        {
            try
            {
                // 检查是否请求停止
                if (stopRequested)
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

                // 识别条形码
                var result = recognitionService.Recognize(fileItem.FilePath);
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
                    fileItem.Status = RecognitionStatus.Failed;
                }
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

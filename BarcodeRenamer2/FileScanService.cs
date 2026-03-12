using System;
using System.Collections.Generic;
using System.IO;

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

        public event EventHandler<FileItem>? FileProcessed;
        public event EventHandler<ScanStatistics>? StatisticsUpdated;

        public FileScanService(AppConfig config)
        {
            this.config = config;
            this.recognitionService = new BarcodeRecognitionService();
            this.processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
        /// 扫描文件夹中的图片文件
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

                            var fileItem = ProcessFile(filePath);

                            // 标记文件为已处理（无论成功或失败）
                            MarkFileAsProcessed(filePath);

                            stats.TotalCount++;

                            if (fileItem.Status == RecognitionStatus.Success)
                            {
                                stats.SuccessCount++;
                            }
                            else if (fileItem.Status == RecognitionStatus.Failed)
                            {
                                stats.FailedCount++;
                            }
                            else if (fileItem.Status == RecognitionStatus.Manual)
                            {
                                stats.ManualCount++;
                            }

                            FileProcessed?.Invoke(this, fileItem);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"处理文件失败 {filePath}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"扫描格式 {format} 失败: {ex.Message}");
                }
            }

            StatisticsUpdated?.Invoke(this, stats);
        }

        /// <summary>
        /// 处理单个文件
        /// </summary>
        private FileItem ProcessFile(string filePath)
        {
            var fileItem = FileItem.FromPath(filePath);

            // 检查文件是否被占用
            if (IsFileLocked(filePath))
            {
                fileItem.Status = RecognitionStatus.Pending;
                return fileItem;
            }

            // 识别条形码
            var result = recognitionService.Recognize(filePath);
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

            return fileItem;
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

                // 移动文件
                File.Move(fileItem.FilePath, newFilePath);
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

        public void Add(ScanStatistics other)
        {
            TotalCount += other.TotalCount;
            SuccessCount += other.SuccessCount;
            FailedCount += other.FailedCount;
            ManualCount += other.ManualCount;
        }
    }
}

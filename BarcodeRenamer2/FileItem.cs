using System;
using System.IO;

namespace BarcodeRenamer2
{
    /// <summary>
    /// 文件信息类
    /// </summary>
    public class FileItem
    {
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 文件类型
        /// </summary>
        public string FileType { get; set; } = string.Empty;

        /// <summary>
        /// 识别状态
        /// </summary>
        public RecognitionStatus Status { get; set; } = RecognitionStatus.Pending;

        /// <summary>
        /// 识别到的条形码内容
        /// </summary>
        public string? BarcodeContent { get; set; }

        /// <summary>
        /// 识别时间
        /// </summary>
        public DateTime RecognitionTime { get; set; }

        /// <summary>
        /// 是否已人工审核
        /// </summary>
        public bool IsManualReview { get; set; } = false;

        /// <summary>
        /// 原始文件路径（用于人工审核时显示原图）
        /// </summary>
        public string? OriginalFilePath { get; set; }

        /// <summary>
        /// 输出文件路径（移动后的新路径）
        /// </summary>
        public string? OutputFilePath { get; set; }

        /// <summary>
        /// 获取格式化的文件大小
        /// </summary>
        public string FormattedSize
        {
            get
            {
                string[] sizes = { "B", "KB", "MB", "GB" };
                double len = FileSize;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
        }

        /// <summary>
        /// 获取状态描述
        /// </summary>
        public string StatusDescription
        {
            get
            {
                return Status switch
                {
                    RecognitionStatus.Pending => "待识别",
                    RecognitionStatus.Recognizing => "识别中...",
                    RecognitionStatus.Success => "识别成功",
                    RecognitionStatus.Failed => "识别失败",
                    RecognitionStatus.Manual => "人工审核",
                    RecognitionStatus.NoBarcode => "无条形码",
                    _ => "未知"
                };
            }
        }

        /// <summary>
        /// 从文件路径创建 FileItem
        /// </summary>
        public static FileItem FromPath(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            return new FileItem
            {
                FileName = fileInfo.Name,
                FilePath = filePath,
                FileSize = fileInfo.Length,
                FileType = fileInfo.Extension.ToUpper().TrimStart('.'),
                Status = RecognitionStatus.Pending
            };
        }
    }

    /// <summary>
    /// 识别状态枚举
    /// </summary>
    public enum RecognitionStatus
    {
        /// <summary>
        /// 待识别
        /// </summary>
        Pending = 0,

        /// <summary>
        /// 识别中
        /// </summary>
        Recognizing = 4,

        /// <summary>
        /// 识别成功
        /// </summary>
        Success = 1,

        /// <summary>
        /// 识别失败
        /// </summary>
        Failed = 2,

        /// <summary>
        /// 人工审核
        /// </summary>
        Manual = 3,

        /// <summary>
        /// 无条形码
        /// </summary>
        NoBarcode = 5
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using ZXing;
using ZXing.Windows.Compatibility;

namespace BarcodeRenamer2
{
    /// <summary>
    /// 条形码识别服务类 - 多线程识别优化版
    /// </summary>
    public class BarcodeRecognitionService
    {
        private readonly BarcodeReader reader;

        public BarcodeRecognitionService()
        {
            reader = new BarcodeReader
            {
                AutoRotate = true,
                TryInverted = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat>
                    {
                        BarcodeFormat.CODE_128,
                        BarcodeFormat.CODE_39,
                        BarcodeFormat.CODE_93,
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.UPC_A,
                        BarcodeFormat.UPC_E,
                        BarcodeFormat.ITF,
                        BarcodeFormat.CODABAR
                    },
                    PureBarcode = false,
                    ReturnCodabarStartEnd = true,
                    TryInverted = true
                }
            };
        }

        /// <summary>
        /// 识别图片中的条形码（多线程优化）
        /// </summary>
        public RecognitionResult Recognize(string imagePath, string? outputFolder = null, string? barcodeContent = null)
        {
            try
            {
                using (var bitmap = new Bitmap(imagePath))
                {
                    // 先去除顶部空白区域
                    using (var noBlank = RemoveTopBlankArea(bitmap))
                    {
                        // 裁剪右上角区域：高度20%，宽度50%（从右侧开始）
                        int cropHeight = noBlank.Height / 5; // 20%高度
                        int cropWidth = (int)(noBlank.Width * 0.5); // 50%宽度
                        using (var cropped = CropTopRightRegion(noBlank, cropHeight, cropWidth))
                        {
                            // 保存裁剪图片
                            if (!string.IsNullOrEmpty(outputFolder))
                            {
                                SaveCroppedImage(cropped, imagePath, outputFolder, barcodeContent);
                            }
                            
                            // 调整DPI到400
                            using (var highDpi = SetHighDPI(cropped, 400))
                            {
                                // 多线程识别策略
                                var result = MultiThreadRecognition(highDpi);
                                if (result != null && ValidateResult(result))
                                {
                                    return CreateSuccessResult(result);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"识别异常: {ex.Message}");
                return new RecognitionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }

            return new RecognitionResult
            {
                Success = false,
                ErrorMessage = "未能识别条形码"
            };
        }
        
        /// <summary>
        /// 保存裁剪图片到输出文件夹的"裁剪"子文件夹
        /// </summary>
        private void SaveCroppedImage(Bitmap cropped, string originalPath, string outputFolder, string? barcodeContent)
        {
            try
            {
                // 创建裁剪文件夹
                string cropFolder = Path.Combine(outputFolder, "裁剪");
                if (!Directory.Exists(cropFolder))
                {
                    Directory.CreateDirectory(cropFolder);
                }
                
                // 生成文件名：优先使用识别的条形码，否则使用原文件名
                string fileName;
                if (!string.IsNullOrEmpty(barcodeContent))
                {
                    // 使用条形码作为文件名
                    string ext = Path.GetExtension(originalPath);
                    fileName = $"{barcodeContent}{ext}";
                    
                    // 如果文件已存在，添加序号
                    int counter = 1;
                    string filePath = Path.Combine(cropFolder, fileName);
                    while (File.Exists(filePath))
                    {
                        fileName = $"{barcodeContent}_{counter}{ext}";
                        filePath = Path.Combine(cropFolder, fileName);
                        counter++;
                    }
                }
                else
                {
                    // 使用原文件名
                    fileName = Path.GetFileName(originalPath);
                }
                
                // 保存裁剪图片
                string savePath = Path.Combine(cropFolder, fileName);
                cropped.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存裁剪图片失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 去除顶部空白区域（增强版 - 多策略检测）
        /// </summary>
        private Bitmap RemoveTopBlankArea(Bitmap original)
        {
            int w = original.Width;
            int h = original.Height;
            
            // 策略1: 检测纯白色空白区域（250-255）
            int firstNonBlankRow1 = DetectBlankByThreshold(original, 250, 5);
            
            // 策略2: 检测接近白色的空白区域（240-255）
            int firstNonBlankRow2 = DetectBlankByThreshold(original, 240, 10);
            
            // 策略3: 检测浅灰色空白区域（230-255）
            int firstNonBlankRow3 = DetectBlankByThreshold(original, 230, 20);
            
            // 策略4: 检测亮度（基于灰度值）
            int firstNonBlankRow4 = DetectBlankByBrightness(original, 240, 15);
            
            // 选择最小的非空白行（最激进的空白去除）
            // 忽略返回0的策略（表示未检测到空白）
            int firstNonBlankRow = h; // 初始化为最大值
            
            if (firstNonBlankRow1 > 0) firstNonBlankRow = Math.Min(firstNonBlankRow, firstNonBlankRow1);
            if (firstNonBlankRow2 > 0) firstNonBlankRow = Math.Min(firstNonBlankRow, firstNonBlankRow2);
            if (firstNonBlankRow3 > 0) firstNonBlankRow = Math.Min(firstNonBlankRow, firstNonBlankRow3);
            if (firstNonBlankRow4 > 0) firstNonBlankRow = Math.Min(firstNonBlankRow, firstNonBlankRow4);
            
            // 如果所有策略都返回0，说明没有空白
            if (firstNonBlankRow == h)
            {
                firstNonBlankRow = 0;
            }
            
            // 如果顶部没有空白，直接返回原图
            if (firstNonBlankRow == 0)
            {
                return new Bitmap(original);
            }
            
            // 裁剪：保留从 firstNonBlankRow 到底部的区域
            int newHeight = h - firstNonBlankRow;
            var cropped = new Bitmap(w, newHeight);
            using (var g = Graphics.FromImage(cropped))
            {
                g.DrawImage(original,
                    new Rectangle(0, 0, w, newHeight),
                    new Rectangle(0, firstNonBlankRow, w, newHeight),
                    GraphicsUnit.Pixel);
            }
            
            System.Diagnostics.Debug.WriteLine($"去除顶部空白: 策略结果[{firstNonBlankRow1},{firstNonBlankRow2},{firstNonBlankRow3},{firstNonBlankRow4}], 选择{firstNonBlankRow}, 原高度{h}, 新高度{newHeight}");
            return cropped;
        }
        
        /// <summary>
        /// 通过阈值检测空白区域
        /// </summary>
        private int DetectBlankByThreshold(Bitmap bitmap, int threshold, int minContentPixels)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            
            for (int y = 0; y < h; y++)
            {
                int nonBlankPixels = 0;
                for (int x = 0; x < w; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    // 检查像素是否接近白色（空白）
                    if (pixel.R < threshold || pixel.G < threshold || pixel.B < threshold)
                    {
                        nonBlankPixels++;
                        if (nonBlankPixels >= minContentPixels)
                        {
                            return y;
                        }
                    }
                }
            }
            
            return 0;
        }
        
        /// <summary>
        /// 通过亮度检测空白区域
        /// </summary>
        private int DetectBlankByBrightness(Bitmap bitmap, int brightnessThreshold, int minContentPixels)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            
            for (int y = 0; y < h; y++)
            {
                int nonBlankPixels = 0;
                for (int x = 0; x < w; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    // 计算亮度（灰度值）
                    int brightness = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    
                    // 如果亮度较低，说明不是空白
                    if (brightness < brightnessThreshold)
                    {
                        nonBlankPixels++;
                        if (nonBlankPixels >= minContentPixels)
                        {
                            return y;
                        }
                    }
                }
            }
            
            return 0;
        }
        
        /// <summary>
        /// 裁剪右上角区域（从顶部开始裁剪）
        /// </summary>
        private Bitmap CropTopRightRegion(Bitmap original, int cropHeight, int cropWidth)
        {
            int w = original.Width;
            int h = original.Height;

            // 确保裁剪尺寸不超过原图尺寸
            cropHeight = Math.Min(cropHeight, h);
            cropWidth = Math.Min(cropWidth, w);

            // 从右侧开始计算裁剪区域（水平方向）
            int startX = w - cropWidth; // 右侧起点
            
            // 从顶部开始裁剪（垂直方向）
            int startY = 0; // 顶部起点

            var cropped = new Bitmap(cropWidth, cropHeight);
            using (var g = Graphics.FromImage(cropped))
            {
                g.DrawImage(original,
                    new Rectangle(0, 0, cropWidth, cropHeight),
                    new Rectangle(startX, startY, cropWidth, cropHeight),
                    GraphicsUnit.Pixel);
            }
            
            System.Diagnostics.Debug.WriteLine($"裁剪右上角区域: 起点({startX}, {startY}), 大小{cropWidth}x{cropHeight}");
            return cropped;
        }
        
        /// <summary>
        /// 验证识别结果的可靠性
        /// </summary>
        private bool ValidateResult(Result result)
        {
            if (result == null || string.IsNullOrEmpty(result.Text))
            {
                return false;
            }
            
            string text = result.Text.Trim();
            
            // 1. 检查识别内容的长度（大多数条形码长度在6-20之间）
            if (text.Length < 6 || text.Length > 20)
            {
                System.Diagnostics.Debug.WriteLine($"识别结果长度不符合要求: {text} (长度: {text.Length})");
                return false;
            }
            
            // 2. 检查识别内容的字符组成（条形码通常是纯数字或字母数字组合）
            int digitCount = 0;
            int letterCount = 0;
            int invalidCharCount = 0;
            
            foreach (char c in text)
            {
                if (char.IsDigit(c))
                {
                    digitCount++;
                }
                else if (char.IsLetter(c))
                {
                    letterCount++;
                }
                else if (c != '-' && c != ' ')
                {
                    // 条形码一般只包含字母、数字、短横线和空格
                    invalidCharCount++;
                }
            }
            
            // 条形码应该以数字为主
            if (digitCount < text.Length * 0.5) // 数字占比至少50%
            {
                System.Diagnostics.Debug.WriteLine($"识别结果数字占比不足: {text} (数字: {digitCount}/{text.Length})");
                return false;
            }
            
            // 包含无效字符
            if (invalidCharCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"识别结果包含无效字符: {text}");
                return false;
            }
            
            // 3. 检查是否全是相同字符（误识别常见模式）
            bool allSame = true;
            for (int i = 1; i < text.Length; i++)
            {
                if (text[i] != text[0])
                {
                    allSame = false;
                    break;
                }
            }
            if (allSame)
            {
                System.Diagnostics.Debug.WriteLine($"识别结果全为相同字符: {text}");
                return false;
            }
            
            // 4. 检查识别结果的置信度（如果有ResultPoints）
            if (result.ResultPoints != null && result.ResultPoints.Length >= 2)
            {
                // 检查识别区域的大小和形状
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                
                foreach (var point in result.ResultPoints)
                {                    if (point == null) continue;
                    if (point.X < minX) minX = point.X;
                    if (point.X > maxX) maxX = point.X;
                    if (point.Y < minY) minY = point.Y;
                    if (point.Y > maxY) maxY = point.Y;
                }
                
                float width = maxX - minX;
                float height = maxY - minY;
                
                // 条形码宽度应该大于高度的1.5倍（条形码通常是横向的）
                if (width < height * 1.5f)
                {
                    System.Diagnostics.Debug.WriteLine($"识别区域形状不符合条形码特征: 宽度={width}, 高度={height}");
                    return false;
                }
                
                // 条形码宽度应该足够大（至少80像素）
                if (width < 80)
                {
                    System.Diagnostics.Debug.WriteLine($"识别区域宽度太小: {width}");
                    return false;
                }
                
                // 高度不应该太大（条形码通常高度较小）
                if (height > 100)
                {
                    System.Diagnostics.Debug.WriteLine($"识别区域高度太大: {height}");
                    return false;
                }
            }
            
            // 5. 检查条形码格式（优先CODE_128和CODE_39）
            // 这些格式更常见于工业应用
            if (result.BarcodeFormat != BarcodeFormat.CODE_128 && 
                result.BarcodeFormat != BarcodeFormat.CODE_39 &&
                result.BarcodeFormat != BarcodeFormat.EAN_13 &&
                result.BarcodeFormat != BarcodeFormat.EAN_8 &&
                result.BarcodeFormat != BarcodeFormat.UPC_A &&
                result.BarcodeFormat != BarcodeFormat.UPC_E &&
                result.BarcodeFormat != BarcodeFormat.ITF)
            {
                System.Diagnostics.Debug.WriteLine($"条形码格式不常见: {result.BarcodeFormat}");
                // 不直接返回false，但记录日志
            }
            
            return true;
        }

        /// <summary>
        /// 设置高DPI（默认400dpi）
        /// </summary>
        private Bitmap SetHighDPI(Bitmap original, int dpi)
        {
            var highDpi = new Bitmap(original.Width, original.Height);
            highDpi.SetResolution(dpi, dpi);

            using (var g = Graphics.FromImage(highDpi))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.DrawImage(original, 0, 0, original.Width, original.Height);
            }

            return highDpi;
        }

        /// <summary>
        /// 多线程识别（3个策略并行）
        /// </summary>
        private Result? MultiThreadRecognition(Bitmap bitmap)
        {
            Result? result = null;
            var tasks = new List<Task<Result?>>();

            // 策略1: 原始识别
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    return reader.Decode(bitmap);
                }
                catch
                {
                    return null;
                }
            }));

            // 策略2: 灰度化 + 二值化
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    using (var gray = ConvertToGrayscale(bitmap))
                    {
                        var r = reader.Decode(gray);
                        if (r != null) return r;

                        // 尝试二值化
                        int[] thresholds = { 128, 100, 150 };
                        foreach (int thresh in thresholds)
                        {
                            using (var binary = Binarize(gray, thresh))
                            {
                                r = reader.Decode(binary);
                                if (r != null) return r;
                            }
                        }
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            }));

            // 策略3: 放大识别
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    int scale = 2;
                    using (var scaled = new Bitmap(bitmap, bitmap.Width * scale, bitmap.Height * scale))
                    {
                        var r = reader.Decode(scaled);
                        if (r != null) return r;

                        using (var gray = ConvertToGrayscale(scaled))
                        {
                            r = reader.Decode(gray);
                            if (r != null) return r;

                            int[] thresholds = { 128, 100 };
                            foreach (int thresh in thresholds)
                            {
                                using (var binary = Binarize(gray, thresh))
                                {
                                    r = reader.Decode(binary);
                                    if (r != null) return r;
                                }
                            }
                        }
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            }));

            // 等待所有任务完成，返回第一个成功的结果
            Task.WaitAll(tasks.ToArray());

            foreach (var task in tasks)
            {
                if (task.Result != null)
                {
                    return task.Result;
                }
            }

            return null;
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        private RecognitionResult CreateSuccessResult(Result result)
        {
            return new RecognitionResult
            {
                Success = true,
                Content = result.Text,
                Format = result.BarcodeFormat.ToString()
            };
        }

        /// <summary>
        /// 转换为灰度图
        /// </summary>
        private Bitmap ConvertToGrayscale(Bitmap original)
        {
            var grayscale = new Bitmap(original.Width, original.Height);

            using (var g = Graphics.FromImage(grayscale))
            {
                var colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                        new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                        new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });

                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(colorMatrix);

                g.DrawImage(original,
                    new Rectangle(0, 0, original.Width, original.Height),
                    0, 0, original.Width, original.Height,
                    GraphicsUnit.Pixel, attributes);
            }

            return grayscale;
        }

        /// <summary>
        /// 二值化处理
        /// </summary>
        private Bitmap Binarize(Bitmap original, int threshold)
        {
            var binary = new Bitmap(original.Width, original.Height);

            for (int x = 0; x < original.Width; x++)
            {
                for (int y = 0; y < original.Height; y++)
                {
                    var pixel = original.GetPixel(x, y);
                    int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    var newColor = gray < threshold ? Color.Black : Color.White;
                    binary.SetPixel(x, y, newColor);
                }
            }

            return binary;
        }
    }

    /// <summary>
    /// 识别结果类
    /// </summary>
    public class RecognitionResult
    {
        public bool Success { get; set; }
        public string? Content { get; set; }
        public string? Format { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

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
        public RecognitionResult Recognize(string imagePath)
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
                            // 调整DPI到400
                            using (var highDpi = SetHighDPI(cropped, 400))
                            {
                                // 多线程识别策略
                                var result = MultiThreadRecognition(highDpi);
                                if (result != null && ValidateResult(result))
                                {
                                    // 关键：验证图像中是否真的存在条形码几何特征
                                    // 防止ZXing在无条形码的图像上产生误识别
                                    if (HasBarcodePattern(highDpi, result))
                                    {
                                        return CreateSuccessResult(result);
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"图像中未检测到条形码特征，可能是误识别: {result.Text}");
                                    }
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
        /// 去除顶部空白区域（增强版 - 分块检测 + 噪点容忍）
        /// </summary>
        private Bitmap RemoveTopBlankArea(Bitmap original)
        {
            int w = original.Width;
            int h = original.Height;
            
            // 策略1: 分块检测 - 每块高度为图片高度的0.5%，容忍少量噪点
            int firstNonBlankRow1 = DetectBlankByBlock(original, 250, 0.005, 0.1); // 块高度0.5%，容忍1%噪点
            
            // 策略2: 分块检测 - 每块高度为图片高度的1%，容忍更多噪点
            int firstNonBlankRow2 = DetectBlankByBlock(original, 240, 0.01, 0.1); // 块高度1%，容忍2%噪点
            
            // 策略3: 分块检测 - 每块高度为图片高度的2%，容忍更多噪点
            int firstNonBlankRow3 = DetectBlankByBlock(original, 230, 0.02, 0.1); // 块高度2%，容忍3%噪点
            
            // 策略4: 分块检测 - 基于亮度
            int firstNonBlankRow4 = DetectBlankByBlockBrightness(original, 240, 0.01, 0.1); // 块高度1%，容忍2%噪点
            
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
        /// 分块检测空白区域（固定宽度，按高度比例分块）
        /// </summary>
        private int DetectBlankByBlock(Bitmap bitmap, int threshold, double blockHeightPercent, double noiseTolerance)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            int blockHeight = Math.Max(1, (int)(h * blockHeightPercent)); // 块高度
            int maxNoisePixels = (int)(w * blockHeight * noiseTolerance); // 整个块的噪点容忍度
            
            // 逐块扫描
            for (int blockStart = 0; blockStart < h; blockStart += blockHeight)
            {
                int blockEnd = Math.Min(blockStart + blockHeight, h);
                int nonBlankPixels = 0;
                
                // 统计整个块的非空白像素
                for (int y = blockStart; y < blockEnd; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        if (pixel.R < threshold || pixel.G < threshold || pixel.B < threshold)
                        {
                            nonBlankPixels++;
                        }
                    }
                }
                
                // 如果该块的非空白像素超过噪点容忍度，说明是内容区域
                if (nonBlankPixels > maxNoisePixels)
                {
                    return blockStart; // 返回块的起始行
                }
            }
            
            return 0;
        }
        
        /// <summary>
        /// 分块检测空白区域（基于亮度）
        /// </summary>
        private int DetectBlankByBlockBrightness(Bitmap bitmap, int brightnessThreshold, double blockHeightPercent, double noiseTolerance)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            int blockHeight = Math.Max(1, (int)(h * blockHeightPercent)); // 块高度
            int maxNoisePixels = (int)(w * blockHeight * noiseTolerance); // 整个块的噪点容忍度
            
            // 逐块扫描
            for (int blockStart = 0; blockStart < h; blockStart += blockHeight)
            {
                int blockEnd = Math.Min(blockStart + blockHeight, h);
                int nonBlankPixels = 0;
                
                // 统计整个块的非空白像素
                for (int y = blockStart; y < blockEnd; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        int brightness = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                        
                        if (brightness < brightnessThreshold)
                        {
                            nonBlankPixels++;
                        }
                    }
                }
                
                // 如果该块的非空白像素超过噪点容忍度，说明是内容区域
                if (nonBlankPixels > maxNoisePixels)
                {
                    return blockStart; // 返回块的起始行
                }
            }
            
            return 0;
        }
        
        /// <summary>
        /// 去除毛边/锯齿，提高条形码清晰度
        /// </summary>
        private Bitmap SmoothImage(Bitmap original)
        {
            int w = original.Width;
            int h = original.Height;
            
            // 创建平滑后的图像
            var smoothed = new Bitmap(w, h);
            
            using (var g = Graphics.FromImage(smoothed))
            {
                // 设置高质量渲染
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                
                // 绘制原图（应用平滑）
                g.DrawImage(original, 0, 0, w, h);
            }
            
            // 应用锐化滤镜（增强条形码边缘）
            using (var sharpened = ApplySharpenFilter(smoothed, 1.5))
            {
                smoothed.Dispose();
                return sharpened;
            }
        }
        
        /// <summary>
        /// 应用锐化滤镜（增强边缘）
        /// </summary>
        private Bitmap ApplySharpenFilter(Bitmap original, double strength)
        {
            int w = original.Width;
            int h = original.Height;
            
            var sharpened = new Bitmap(w, h);
            
            // 锐化卷积核
            double[,] kernel = {
                { 0, -1, 0 },
                { -1, 4 + strength, -1 },
                { 0, -1, 0 }
            };
            
            // 应用卷积
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    double r = 0, g = 0, b = 0;
                    
                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            var pixel = original.GetPixel(x + kx, y + ky);
                            double k = kernel[ky + 1, kx + 1];
                            r += pixel.R * k;
                            g += pixel.G * k;
                            b += pixel.B * k;
                        }
                    }
                    
                    // 限制RGB值范围
                    int newR = Math.Min(255, Math.Max(0, (int)r));
                    int newG = Math.Min(255, Math.Max(0, (int)g));
                    int newB = Math.Min(255, Math.Max(0, (int)b));
                    
                    sharpened.SetPixel(x, y, Color.FromArgb(newR, newG, newB));
                }
            }
            
            // 复制边缘像素
            for (int x = 0; x < w; x++)
            {
                sharpened.SetPixel(x, 0, original.GetPixel(x, 0));
                sharpened.SetPixel(x, h - 1, original.GetPixel(x, h - 1));
            }
            for (int y = 0; y < h; y++)
            {
                sharpened.SetPixel(0, y, original.GetPixel(0, y));
                sharpened.SetPixel(w - 1, y, original.GetPixel(w - 1, y));
            }
            
            return sharpened;
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
        /// 验证识别结果的可靠性（增强版）
        /// 关键：验证图像中是否真的存在条形码的几何特征（黑白条纹模式）
        /// </summary>
        private bool ValidateResult(Result result)
        {
            if (result == null || string.IsNullOrEmpty(result.Text))
            {
                return false;
            }
            
            string text = result.Text.Trim();
            
            // 1. 检查识别内容的长度（大多数条形码长度在8-20之间）
            if (text.Length < 8 || text.Length > 20)
            {
                System.Diagnostics.Debug.WriteLine($"识别结果长度不符合要求: {text} (长度: {text.Length})");
                return false;
            }
            
            // 2. 检查识别内容的字符组成（条形码通常是纯数字）
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
                    invalidCharCount++;
                }
            }
            
            // 条形码应该绝大部分是数字（至少80%）
            if (digitCount < text.Length * 0.8)
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
            
            // 4. 检查是否是连续重复模式（如123123, ABCABC）
            if (IsRepeatingPattern(text))
            {
                System.Diagnostics.Debug.WriteLine($"识别结果是重复模式: {text}");
                return false;
            }
            
            // 5. 检查识别结果的置信度（如果有ResultPoints）
            if (result.ResultPoints != null && result.ResultPoints.Length >= 2)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                
                foreach (var point in result.ResultPoints)
                {
                    if (point == null) continue;
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
        /// 验证图像区域是否真的包含条形码特征（黑白条纹模式）
        /// 这是防止误识别的关键：即使ZXing返回了结果，也要检查图像本身
        /// </summary>
        private bool HasBarcodePattern(Bitmap bitmap, Result result)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            
            // 如果有ResultPoints，分析识别区域
            if (result.ResultPoints != null && result.ResultPoints.Length >= 2)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                
                foreach (var point in result.ResultPoints)
                {
                    if (point == null) continue;
                    if (point.X < minX) minX = point.X;
                    if (point.X > maxX) maxX = point.X;
                    if (point.Y < minY) minY = point.Y;
                    if (point.Y > maxY) maxY = point.Y;
                }
                
                // 扩展检测区域（确保覆盖完整条形码）
                int padding = 5;
                int startX = Math.Max(0, (int)minX - padding);
                int endX = Math.Min(w - 1, (int)maxX + padding);
                int startY = Math.Max(0, (int)minY - padding);
                int endY = Math.Min(h - 1, (int)maxY + padding);
                
                return AnalyzeBarcodeRegion(bitmap, startX, endX, startY, endY);
            }
            
            // 没有ResultPoints，分析整个图像的关键区域
            // 条形码通常在图像的某个角落或边缘
            return AnalyzeKeyRegions(bitmap);
        }
        
        /// <summary>
        /// 分析指定区域是否包含条形码特征
        /// </summary>
        private bool AnalyzeBarcodeRegion(Bitmap bitmap, int startX, int endX, int startY, int endY)
        {
            int regionWidth = endX - startX + 1;
            int regionHeight = endY - startY + 1;
            
            if (regionWidth < 50 || regionHeight < 10)
            {
                System.Diagnostics.Debug.WriteLine($"检测区域太小: {regionWidth}x{regionHeight}");
                return false;
            }
            
            // 转换为灰度并二值化
            int threshold = 128;
            int[,] binaryPixels = new int[regionHeight, regionWidth];
            int blackCount = 0;
            int whiteCount = 0;
            
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                    int binary = gray < threshold ? 0 : 1;
                    binaryPixels[y - startY, x - startX] = binary;
                    if (binary == 0) blackCount++;
                    else whiteCount++;
                }
            }
            
            // 1. 检查黑白像素比例（条形码应该大致平衡，30%-70%黑色）
            double blackRatio = (double)blackCount / (blackCount + whiteCount);
            if (blackRatio < 0.15 || blackRatio > 0.85)
            {
                System.Diagnostics.Debug.WriteLine($"黑白像素比例不符合条形码特征: 黑色占比 {blackRatio:P1}");
                return false;
            }
            
            // 2. 检测条纹模式：分析每一行的黑白交替次数
            // 条形码每一行都应该有密集的黑白交替
            int validRows = 0;
            int totalRows = regionHeight;
            int minTransitions = 15; // 条形码至少有15次黑白交替（30条线）
            
            for (int y = 0; y < regionHeight; y++)
            {
                int transitions = 0;
                for (int x = 1; x < regionWidth; x++)
                {
                    if (binaryPixels[y, x] != binaryPixels[y, x - 1])
                    {
                        transitions++;
                    }
                }
                
                // 条形码行应该有足够的黑白交替
                if (transitions >= minTransitions)
                {
                    validRows++;
                }
            }
            
            // 至少80%的行应该有足够的条纹交替
            double validRowRatio = (double)validRows / totalRows;
            if (validRowRatio < 0.8)
            {
                System.Diagnostics.Debug.WriteLine($"条纹行比例不足: {validRows}/{totalRows} ({validRowRatio:P1})");
                return false;
            }
            
            // 3. 检测条纹的一致性：相邻行的条纹位置应该对齐
            if (regionHeight >= 2)
            {
                int consistentPairs = 0;
                int totalPairs = regionHeight - 1;
                
                for (int y = 0; y < regionHeight - 1; y++)
                {
                    int samePositions = 0;
                    for (int x = 0; x < regionWidth; x++)
                    {
                        if (binaryPixels[y, x] == binaryPixels[y + 1, x])
                        {
                            samePositions++;
                        }
                    }
                    
                    double similarity = (double)samePositions / regionWidth;
                    if (similarity > 0.7) // 70%相似度
                    {
                        consistentPairs++;
                    }
                }
                
                double consistency = (double)consistentPairs / totalPairs;
                if (consistency < 0.7)
                {
                    System.Diagnostics.Debug.WriteLine($"条纹一致性不足: {consistency:P1}");
                    return false;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"条形码特征验证通过: 黑色占比 {blackRatio:P1}, 有效行 {validRows}/{totalRows}");
            return true;
        }
        
        /// <summary>
        /// 分析图像关键区域是否存在条形码特征
        /// </summary>
        private bool AnalyzeKeyRegions(Bitmap bitmap)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            
            // 检查几个可能的条形码位置
            // 右上角、左上角、顶部中央
            var regions = new[]
            {
                new { startX = w / 2, endX = w - 1, startY = 0, endY = h / 3 },           // 右上角
                new { startX = 0, endX = w / 2, startY = 0, endY = h / 3 },              // 左上角
                new { startX = w / 4, endX = w * 3 / 4, startY = 0, endY = h / 4 },      // 顶部中央
            };
            
            foreach (var region in regions)
            {
                if (AnalyzeBarcodeRegion(bitmap, region.startX, region.endX, region.startY, region.endY))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 检查是否是重复模式（如123123, ABCABC）
        /// </summary>
        private bool IsRepeatingPattern(string text)
        {
            if (text.Length < 6) return false;
            
            // 检查2字符重复模式（如ABABAB）
            if (text.Length >= 6 && text.Length % 2 == 0)
            {
                string pattern2 = text.Substring(0, 2);
                bool isPattern2 = true;
                for (int i = 2; i < text.Length; i += 2)
                {
                    if (text.Substring(i, 2) != pattern2)
                    {
                        isPattern2 = false;
                        break;
                    }
                }
                if (isPattern2) return true;
            }
            
            // 检查3字符重复模式（如ABCABC）
            if (text.Length >= 6 && text.Length % 3 == 0)
            {
                string pattern3 = text.Substring(0, 3);
                bool isPattern3 = true;
                for (int i = 3; i < text.Length; i += 3)
                {
                    if (text.Substring(i, 3) != pattern3)
                    {
                        isPattern3 = false;
                        break;
                    }
                }
                if (isPattern3) return true;
            }
            
            // 检查4字符重复模式
            if (text.Length >= 8 && text.Length % 4 == 0)
            {
                string pattern4 = text.Substring(0, 4);
                bool isPattern4 = true;
                for (int i = 4; i < text.Length; i += 4)
                {
                    if (text.Substring(i, 4) != pattern4)
                    {
                        isPattern4 = false;
                        break;
                    }
                }
                if (isPattern4) return true;
            }
            
            return false;
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

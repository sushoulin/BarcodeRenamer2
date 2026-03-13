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
                    // 裁剪高度20%区域
                    int cropHeight = bitmap.Height / 5; // 20%
                    using (var cropped = CropTopRegion(bitmap, cropHeight))
                    {
                        // 保存裁剪图片
                        if (!string.IsNullOrEmpty(outputFolder))
                        {
                            SaveCroppedImage(cropped, imagePath, outputFolder, barcodeContent);
                        }
                        
                        // 调整DPI到150以上
                        using (var highDpi = SetHighDPI(cropped, 200))
                        {
                            // 多线程识别策略
                            var result = MultiThreadRecognition(highDpi);
                            if (result != null)
                            {
                                return CreateSuccessResult(result);
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
        /// 裁剪顶部区域（高度20%）
        /// </summary>
        private Bitmap CropTopRegion(Bitmap original, int cropHeight)
        {
            int w = original.Width;
            int h = original.Height;

            // 确保裁剪高度不超过原图高度
            cropHeight = Math.Min(cropHeight, h);

            var cropped = new Bitmap(w, cropHeight);
            using (var g = Graphics.FromImage(cropped))
            {
                g.DrawImage(original,
                    new Rectangle(0, 0, w, cropHeight),
                    new Rectangle(0, 0, w, cropHeight),
                    GraphicsUnit.Pixel);
            }
            return cropped;
        }

        /// <summary>
        /// 设置高DPI（默认200dpi）
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

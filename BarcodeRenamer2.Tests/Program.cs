using System;
using System.Drawing;
using System.IO;
using ZXing;
using ZXing.Windows.Compatibility;

namespace BarcodeRenamer2.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== 条形码识别测试 ===\n");

            var testImages = new[]
            {
                new { File = "test_barcodes/01137590.png", Expected = "40260300081" },
                new { File = "test_barcodes/11264071.png", Expected = "40260300085" }
            };

            var service = new BarcodeRecognitionService();

            foreach (var test in testImages)
            {
                Console.WriteLine($"测试文件: {test.File}");
                Console.WriteLine($"预期结果: {test.Expected}");

                string fullPath = Path.Combine(Directory.GetCurrentDirectory(), test.File);

                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"❌ 文件不存在: {fullPath}\n");
                    continue;
                }

                // 获取图片信息
                using (var img = new Bitmap(fullPath))
                {
                    Console.WriteLine($"图片尺寸: {img.Width} x {img.Height}");
                }

                // 测试识别
                var result = service.Recognize(fullPath);

                if (result.Success)
                {
                    Console.WriteLine($"✅ 识别成功: {result.Content}");
                    Console.WriteLine($"   格式: {result.Format}");
                    Console.WriteLine($"   匹配: {(result.Content == test.Expected ? "✅ 正确" : "❌ 错误")}");
                }
                else
                {
                    Console.WriteLine($"❌ 识别失败: {result.ErrorMessage}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("=== 测试完成 ===");
        }
    }
}

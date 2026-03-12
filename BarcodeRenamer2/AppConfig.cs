using Newtonsoft.Json;
using System;
using System.IO;

namespace BarcodeRenamer2
{
    /// <summary>
    /// 应用程序配置类
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 扫描文件夹路径
        /// </summary>
        public string ScanFolder { get; set; } = string.Empty;

        /// <summary>
        /// 输出文件夹路径
        /// </summary>
        public string OutputFolder { get; set; } = string.Empty;

        /// <summary>
        /// 扫描间隔（毫秒）
        /// </summary>
        public int ScanInterval { get; set; } = 2000;

        /// <summary>
        /// 是否自动扫描
        /// </summary>
        public bool AutoScan { get; set; } = false;

        /// <summary>
        /// 支持的图片格式
        /// </summary>
        public string[] SupportedFormats { get; set; } = new string[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff" };

        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BarcodeRenamer2",
            "config.json"
        );

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        /// <returns>配置对象</returns>
        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
            }

            return new AppConfig();
        }
    }
}

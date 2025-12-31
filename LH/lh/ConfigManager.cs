using Newtonsoft.Json;
using Rongmeng_20251223.Interface.Model;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Rongmeng_20251223.LH.lh
{
    // 1. 加上 static，表明这是个纯工具类，不需要 new ConfigManager()
    public static class ConfigManager
    {
        private static string GetConfigPath()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string projectName = Assembly.GetExecutingAssembly().GetName().Name;

            return Path.Combine(basePath, $"{projectName}.json");
        }

        // 保存配置
        public static void Save(AppConfig config)
        {
            try
            {
                string filePath = GetConfigPath();
                string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(filePath, jsonContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("保存配置失败: " + ex.Message);
            }
        }

        // 读取配置
        public static AppConfig Load()
        {
            string filePath = GetConfigPath();

            // 如果文件不存在，直接返回一个新的默认对象，不要报错
            if (!File.Exists(filePath))
            {
                return new AppConfig();
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
                var config = JsonConvert.DeserializeObject<AppConfig>(jsonContent);
                // 防止文件内容为空导致 config 为 null
                return config ?? new AppConfig();
            }
            catch
            {
                // 如果 JSON 格式坏了，也返回默认对象，保证程序不崩
                return new AppConfig();
            }
        }
    }
}
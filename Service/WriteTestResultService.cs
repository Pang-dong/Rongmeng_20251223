using CommunityToolkit.Mvvm.Messaging;
using FFmpeg.AutoGen;
using Newtonsoft.Json.Linq;
using Rongmeng_20251223.Interface.Model;
using Rongmeng_20251223.LH.lh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rongmeng_20251223.Service
{
    public  class WriteTestResultService
    {
        /// <summary>
        /// 包装/增强 JSON 数据，添加额外字段
        /// </summary>
        /// <param name="originalJson">原始的测试结果 JSON 字符串</param>
        /// <returns>包含额外信息的新 JSON 字符串</returns>
        public  string EnrichJsonData(string originalJson)
        {
            try
            {
                JObject jsonObject = JObject.Parse(originalJson);
                var config = ConfigManager.Load();
                jsonObject.Add("Operator", config.UserName);       // 操作员
                jsonObject.Add("StationName", config.LastStation); // 工站名称

                jsonObject.Add("DataType", "FinalResult");

                return jsonObject.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex)
            {
                AddLog($"JSON 组装失败: {ex.Message}");
                return originalJson; // 如果出错，至少返回原始数据
            }
        }
        private void AddLog(string msg) => WeakReferenceMessenger.Default.Send(new Messages(msg));
    }
}

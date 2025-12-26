using System;
using System.Threading.Tasks;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Rongmeng_20251223.Service // 请修改为你的实际命名空间
{
    public class LicenseService
    {
        // 定义日志记录器 (假设你使用了log4net，如果没有可以删除)
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #region 配置常量

        /// <summary>
        /// API密钥，用于许可证验证
        /// </summary>
        public static string ApiKey => "b22fef2a99b2b70096a09c9a48330abc";

        /// <summary>
        /// 许可证服务器地址
        /// </summary>
        public static string LicenseServer => "http://8.138.225.231/api/v1/code";

        /// <summary>
        /// 批处理ID，用于许可证请求
        /// </summary>
        public static string BatchId => "c8068398-4c99-4d3f-abff-73bc2a2c81ba";

        #endregion

        /// <summary>
        /// 同步获取授权码
        /// </summary>
        /// <param name="deviceUid">设备UID</param>
        /// <returns>成功返回授权码字符串，失败返回 null</returns>
        public string GetLicenseCode(string deviceUid)
        {
            if (string.IsNullOrWhiteSpace(deviceUid))
            {
                logger.Warn("请求授权失败：DeviceUID 为空");
                return null;
            }
            try
            {
                // 1. 创建客户端
                var client = new RestClient(LicenseServer);
                client.Timeout = 5000; // 设置5秒超时

                // 2. 创建请求
                var request = new RestRequest(Method.POST);

                // 添加 Header
                request.AddHeader("x-api-key", ApiKey);
                request.AddHeader("Content-Type", "application/json");
                var bodyObj = new
                {
                    batch_id = BatchId,
                    device_id = deviceUid
                };
                string jsonBody = JsonConvert.SerializeObject(bodyObj);
                request.AddParameter("application/json", jsonBody, ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);
                if (response.IsSuccessful)
                {
                    JObject json = JObject.Parse(response.Content);
                    bool success = (bool)json["success"];

                    if (success)
                    {
                        string licenseData = json["data"]?.ToString();
                        logger.Info($"授权码获取成功，UID: {deviceUid}");
                        return licenseData;
                    }
                    else
                    {
                        string msg = json["msg"]?.ToString() ?? "未知错误";
                        logger.Warn($"服务器拒绝授权: {msg}");
                    }
                }
                else
                {
                    logger.Error($"网络请求失败: Code={response.StatusCode}, Error={response.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                logger.Error("获取授权码过程中发生异常", ex);
            }

            return null;
        }

        /// <summary>
        /// 异步获取授权码 (推荐用于 WPF/WinForm 防止界面卡死)
        /// </summary>
        public async Task<string> GetLicenseCodeAsync(string deviceUid)
        {
            return await Task.Run(() => GetLicenseCode(deviceUid));
        }
    }
}

using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rongmeng_20251223.Interface.Model
{
    public class WebApiHelper
    {
        private static string _token;

        private const string DefaultUser = "admin";
        private const string DefaultPassword = "123456";
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static string _baseUrl;
        static WebApiHelper()
        {
            _baseUrl = GetAvailableBaseUrl();
        }

        /// <summary>
        /// 获取设备信息
        /// </summary>
        public static async Task<string> GetDeviceInfoJsonAsync(string barcode, CancellationToken cancellationToken = default)
        {
            string token = await GetTokenAsync(cancellationToken);

            using (var httpClient = new HttpClient())
            {
                logger.Debug($"获取设备信息_Web_api地址为: {_baseUrl}");
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                string url = $"{_baseUrl}/api/Home/GetMaterialSn?BARCODE={Uri.EscapeDataString(barcode)}";

                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken);//这个传了默认参数
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (TaskCanceledException ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        logger.Debug("设备信息请求被用户取消");
                        throw new OperationCanceledException("设备信息请求被取消", ex, cancellationToken);
                    }
                    else
                    {
                        logger.Debug($"连接 {_baseUrl} 超时");
                        throw new TimeoutException($"连接 {_baseUrl} 超时", ex);
                    }
                }
            }
        }
        /// <summary>
        /// 上传测试结果 (对应接口 WriteTestResult)
        /// </summary>
        /// <param name="writeTestResultJson">测试结果的JSON字符串</param>
        public static async Task<string> WriteTestResultAsync(string writeTestResultJson, CancellationToken cancellationToken = default)
        {
            // 1. 获取 Token
            string token = await GetTokenAsync(cancellationToken);

            using (var httpClient = new HttpClient())
            {
                logger.Debug($"上传测试结果_Web_api地址为: {_baseUrl}");
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                // 添加鉴权 Header
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                string url = $"{_baseUrl}/api/Home/WriteTestResultInfo?writeTestResult={Uri.EscapeDataString(writeTestResultJson)}";

                try
                {
                    HttpResponseMessage response = await httpClient.PostAsync(url, null, cancellationToken);

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (TaskCanceledException ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        logger.Debug("上传测试结果请求被用户取消");
                        throw new OperationCanceledException("上传测试结果请求被取消", ex, cancellationToken);
                    }
                    else
                    {
                        logger.Debug($"连接 {_baseUrl} 超时");
                        throw new TimeoutException($"连接 {_baseUrl} 超时", ex);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"上传测试结果失败: {ex.Message}", ex);
                    throw;
                }
            }
        }
        /// <summary>
        /// 获取产品相关码信息
        /// </summary>
        /// <param name="sn">SN码</param>
        /// <param name="testType">测试类型</param>
        public static async Task<string> GetSNCodeInfoAsync(string sn, string testType, CancellationToken cancellationToken = default)
        {
            // 1. 获取 Token
            string token = await GetTokenAsync(cancellationToken);

            using (var httpClient = new HttpClient())
            {
                logger.Debug($"获取产品码信息_Web_api地址为: {_baseUrl}");
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                string url = $"{_baseUrl}/api/Home/GetSNCodeInfo";
                var requestData = new
                {
                    sn = sn,
                    testType = testType
                };

                string jsonBody = JsonConvert.SerializeObject(requestData);

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                {
                    try
                    {
                        // 发送 POST 请求
                        HttpResponseMessage response = await httpClient.PostAsync(url, content, cancellationToken);

                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }
                    catch (TaskCanceledException ex)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            logger.Debug("获取产品码信息请求被用户取消");
                            throw new OperationCanceledException("获取产品码信息请求被取消", ex, cancellationToken);
                        }
                        else
                        {
                            logger.Debug($"连接 {_baseUrl} 超时");
                            throw new TimeoutException($"连接 {_baseUrl} 超时", ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"获取产品码信息失败: {ex.Message}", ex);
                        throw;
                    }
                }
            }
        }
        /// <summary>

        /// 校验许可证信息
        /// </summary>
        public static async Task<string> GetLisenceInfonAsync(string barcode, CancellationToken cancellationToken = default)
        {
            string token = await GetTokenAsync(cancellationToken);

            using (var httpClient = new HttpClient())
            {
                logger.Debug($"获取设备信息_Web_api地址为: {_baseUrl}");
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                string url = $"{_baseUrl}/api/Home/GetLicense";

                var content = new StringContent(barcode, Encoding.UTF8, "application/json");

                try
                {
                    HttpResponseMessage response = await httpClient.PostAsync(url, content, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (TaskCanceledException ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        logger.Debug("设备信息请求被用户取消");
                        throw new OperationCanceledException("设备信息请求被取消", ex, cancellationToken);
                    }
                    else
                    {
                        logger.Debug($"连接 {_baseUrl} 超时");
                        throw new TimeoutException($"连接 {_baseUrl} 超时", ex);
                    }
                }
            }
        }
        /// <summary>
        /// 获取Token
        /// </summary>
        private static async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(_token))
            {
                return _token;
            }

            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                logger.Debug($"获取Token_Web_api地址为:{_baseUrl}");
                string url = $"{_baseUrl}/api/Login/GetToken?User={DefaultUser}&Password={DefaultPassword}";

                HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                string token = await response.Content.ReadAsStringAsync();
                _token = token.Trim('"');
                return _token;
            }
        }

        /// <summary>
        /// 获取可用的基础URL
        /// </summary>
        private static string GetAvailableBaseUrl()
        {
            string primaryUrl = "http://192.168.88.144:5673";
            string secondaryUrl = "http://192.168.1.3:5673";

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(2);
                    var response = client.GetAsync($"{primaryUrl}/api/Login/GetToken").GetAwaiter().GetResult();
                    logger.Debug($"使用主地址: {primaryUrl}");
                    return primaryUrl;
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"主地址不可用: {ex.Message}, 使用备用地址: {secondaryUrl}");
                return secondaryUrl;
            }
        }

        public static async Task<string> GetUserLoginInfo(string userName, string password, CancellationToken cancellationToken = default)
        {
            string token = await GetTokenAsync(cancellationToken);

            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                string url = $"{_baseUrl}/api/Home/GetUserLoginInfo";
                var requestData = new { _userName = userName, _password = password };
                string jsonBody = JsonConvert.SerializeObject(requestData);
                logger.Info($"用户登录验证: {userName}");

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                {
                    try
                    {
                        HttpResponseMessage response = await httpClient.PostAsync(url, content, cancellationToken);
                        response.EnsureSuccessStatusCode();
                        string result = await response.Content.ReadAsStringAsync();
                        logger.Info($"用户登录验证成功: {userName}");
                        return result;
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"用户登录验证失败: {userName}", ex);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// 清除Token
        /// </summary>
        public static void ClearToken()
        {
            _token = null;
        }
    }
}
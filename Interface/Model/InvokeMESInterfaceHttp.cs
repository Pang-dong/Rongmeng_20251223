using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Rongmeng_20251223.Interface.Model
{
    public static class InvokeMESInterfaceHttp
    {
        // 复用 HttpClient 实例，避免端口耗尽
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// 新的接口调用方法
        /// 建议将 args 改为 Dictionary<string, object> 以便匹配参数名
        /// </summary>
        public static string MESInterface(string methodName, Dictionary<string, object> parameters)
        {
            // 这里为了保持和你原有代码签名一致的返回逻辑，我们做个同步包装
            // 实际项目中建议一路 async/await
            return Task.Run(() => CallSoapServiceAsync(methodName, parameters)).GetAwaiter().GetResult();
        }

        private static async Task<string> CallSoapServiceAsync(string methodName, Dictionary<string, object> parameters)
        {
            string ip = ""; // 假设这是你项目里能获取到的IP
            string url = $"http://{ip}:8017/Service.asmx";

            string soapNamespace = "http://tempuri.org/";

            try
            {
                // 1. 构建 SOAP XML 包体
                string soapBody = BuildSoapEnvelope(methodName, parameters, soapNamespace);
                var content = new StringContent(soapBody, Encoding.UTF8, "text/xml");

                // 2. 添加 SOAPAction 头 (ASMX 通常需要)
                if (!_httpClient.DefaultRequestHeaders.Contains("SOAPAction"))
                {
                    _httpClient.DefaultRequestHeaders.Add("SOAPAction", $"{soapNamespace}{methodName}");
                }
                // 注意：HttpClient 是单例，Header 可能会累积，严谨做法是每次 Request Message 单独设置 Header
                // 这里为了简化演示，使用 HttpRequestMessage 会更好：
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Add("SOAPAction", $"{soapNamespace}{methodName}");
                    request.Content = content;

                    // 3. 发送请求
                    using (var response = await _httpClient.SendAsync(request))
                    {
                        // 4. 读取响应
                        string responseString = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new Exception($"HTTP Error {response.StatusCode}: {responseString}");
                        }

                        // 5. 解析 XML 结果
                        return ParseSoapResponse(responseString, methodName, soapNamespace);
                    }
                }
            }
            catch (Exception ex)
            {
                // 保持原有的错误返回格式
                var productInfo = new
                {
                    SNCount = 0,
                    SNStatus = "接口调用错误",
                    msg = ex.Message,
                    SNCodeInfo = new List<object>(), // 使用 object 或你原本的 DeviceInfo
                };
                return JsonConvert.SerializeObject(productInfo);
            }
        }

        /// <summary>
        /// 构建 SOAP XML 字符串
        /// </summary>
        private static string BuildSoapEnvelope(string methodName, Dictionary<string, object> args, string ns)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">");
            sb.Append("<soap:Body>");

            // 方法名
            sb.Append($"<{methodName} xmlns=\"{ns}\">");

            // 参数列表
            if (args != null)
            {
                foreach (var kvp in args)
                {
                    sb.Append($"<{kvp.Key}>{kvp.Value}</{kvp.Key}>");
                }
            }

            sb.Append($"</{methodName}>");
            sb.Append("</soap:Body>");
            sb.Append("</soap:Envelope>");
            return sb.ToString();
        }

        /// <summary>
        /// 解析 SOAP XML 返回值
        /// </summary>
        private static string ParseSoapResponse(string xml, string methodName, string ns)
        {
            try
            {
                XDocument doc = XDocument.Parse(xml);
                XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
                XNamespace serviceNs = ns;

                // ASMX 默认返回结果标签为：方法名 + Result
                string resultNodeName = methodName + "Result";

                var resultNode = doc.Descendants(serviceNs + resultNodeName).FirstOrDefault();

                // 如果找不到带 namespace 的节点，尝试不带 namespace 查找
                if (resultNode == null)
                {
                    resultNode = doc.Descendants().FirstOrDefault(n => n.Name.LocalName == resultNodeName);
                }

                return resultNode?.Value ?? "";
            }
            catch
            {
                // 如果解析 XML 失败，直接返回原始内容用于调试
                return xml;
            }
        }
    }
}

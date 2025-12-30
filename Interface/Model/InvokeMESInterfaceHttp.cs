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
    public static class InvokeMESInterface
    {
        // 复用 HttpClient，避免端口耗尽
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// 通用 MES 接口调用方法 (支持任意方法、任意参数)
        /// </summary>
        /// <param name="url">完整接口地址 (如 http://192.168.1.100:8017/Service.asmx)</param>
        /// <param name="methodName">方法名 (如 GetUserLoginInfo)</param>
        /// <param name="parameters">参数字典</param>
        /// <param name="soapNamespace">命名空间 (默认为 http://tempuri.org/)</param>
        /// <returns>返回接口结果字符串</returns>
        public static async Task<string> PostToMesAsync(
            string url,
            string methodName,
            Dictionary<string, object> parameters,
            string soapNamespace = "http://tempuri.org/")
        {
            try
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(3);
                string soapBody = BuildSoapEnvelope(methodName, parameters, soapNamespace);
                var content = new StringContent(soapBody, Encoding.UTF8, "text/xml");
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Add("SOAPAction", $"{soapNamespace}{methodName}");
                    request.Content = content;

                    // 4. 发送并等待结果
                    using (var response = await _httpClient.SendAsync(request))
                    {
                        string responseString = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            return $"ERROR: HTTP {response.StatusCode} - {responseString}";
                        }

                        // 5. 解析 XML 返回值
                        return ParseSoapResponse(responseString, methodName, soapNamespace);
                    }
                }
            }
            catch (Exception ex)
            {
                return $"ERROR: 接口异常 - {ex.Message}";
            }
        }

        /// <summary>
        /// 构造 SOAP XML 字符串
        /// </summary>
        private static string BuildSoapEnvelope(string methodName, Dictionary<string, object> args, string ns)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.Append("<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">");
            sb.Append("<soap:Body>");

            // 方法名节点
            sb.Append($"<{methodName} xmlns=\"{ns}\">");

            // 参数节点
            if (args != null)
            {
                foreach (var kvp in args)
                {
                    // 注意：这里直接使用 Key 作为标签名，确保传入的 Key 与 WSDL 定义一致
                    sb.Append($"<{kvp.Key}>{kvp.Value}</{kvp.Key}>");
                }
            }

            sb.Append($"</{methodName}>");
            sb.Append("</soap:Body>");
            sb.Append("</soap:Envelope>");
            return sb.ToString();
        }

        /// <summary>
        /// 解析 SOAP XML 响应
        /// </summary>
        private static string ParseSoapResponse(string xml, string methodName, string ns)
        {
            try
            {
                XDocument doc = XDocument.Parse(xml);
                string resultNodeName = methodName + "Result";
                var resultNode = doc.Descendants().FirstOrDefault(n => n.Name.LocalName == resultNodeName);
                return resultNode?.Value ?? "";
            }
            catch (Exception ex)
            {
                return $"ERROR: XML解析失败 - {ex.Message}";
            }
        }
    }
}

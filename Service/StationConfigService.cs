using Newtonsoft.Json;
using Rongmeng_20251223.Interface.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rongmeng_20251223.Service
{
    public class StationConfigService
    {
        public List<StationTestItem> LoadStationConfig(string stationName)
        {
            var result = new List<StationTestItem>();
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chinese", "StationConfig.json");

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"配置文件未找到: {configPath}");
            }

            try
            {
                string json = File.ReadAllText(configPath);
                var allItems = JsonConvert.DeserializeObject<List<StationTestItem>>(json);

                if (allItems != null)
                {
                    // 筛选当前工站的配置
                    result = allItems.FindAll(item => item.Station == stationName);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"解析配置文件失败: {ex.Message}");
            }

            return result;
        }
    }
}

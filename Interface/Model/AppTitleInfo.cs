using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rongmeng_20251223.Interface.Model
{
    public class AppTitleInfo
    {
        public string ProgramName { get; set; } = "联合测试工具";

        // 2. 项目名称
        public string ProjectName { get; set; } = "融梦X5-20251230";

        // 3. 版本号
        public string Version { get; set; } = "V1.0.0";

        /// <summary>
        /// 拼接后的完整标题，格式示例：融梦智能控制系统 - X5检测项目 V1.0.0
        /// </summary>
        public string FullTitle
        {
            get
            {
                return $"{ProgramName} - {ProjectName}-{Version}";
            }
        }
    }
}

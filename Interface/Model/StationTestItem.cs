using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rongmeng_20251223.Interface.Model
{
    public  class StationTestItem
    {
        public string Station { get; set; }      // 工站名称
        public string Title { get; set; }        // 按钮标题
        public string Command { get; set; }      // 十六进制指令
        public string Tips { get; set; }         // 操作提示
        public int Timeout { get; set; }         // 防呆时间(秒)
        public string MesName { get; set; }
    }
}

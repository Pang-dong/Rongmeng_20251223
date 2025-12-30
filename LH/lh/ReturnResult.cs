using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rongmeng_20251223.LH.lh
{
    public  class ReturnResult
    {
        public class BaseResult
        {
            // 对应 JSON 中的 status 字段
            public string status { get; set; }

            // 对应 JSON 中的 msg 字段
            public string msg { get; set; }

            // 辅助属性：判断是否成功 (假设 1 是成功)
            public bool IsSuccess => status == "1";
        }
    }
}

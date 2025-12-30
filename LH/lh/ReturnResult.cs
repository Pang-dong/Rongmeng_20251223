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
            public string status { get; set; }

            public string msg { get; set; }
            public bool IsSuccess => status == "1";
        }
    }
}

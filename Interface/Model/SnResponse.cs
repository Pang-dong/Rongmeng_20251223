using System.Collections.Generic;

namespace Rongmeng_20251223.Interface.Model
{
    public class SnCodeInfo { }

    public class SnResponse
    {
        public List<SnCodeInfo> snCodeInfo { get; set; }
        public string snStatus { get; set; }
        public string msg { get; set; }
    }
}

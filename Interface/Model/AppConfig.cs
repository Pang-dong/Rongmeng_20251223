using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rongmeng_20251223.Interface.Model
{
    public  class AppConfig
    {
        public string UserName { get; set; } = "";
        public string Password { get; set; } = ""; 
        public string LastStation { get; set; } = ""; // 上一次选择的工站
        public bool IsRememberMe { get; set; } = false; // 是否记住密码
        public bool IsMesMode { get; set; } = true;     // 上次选择的模式
        public string FtpIp { get; set; } = "192.168.88.144";
    }
}

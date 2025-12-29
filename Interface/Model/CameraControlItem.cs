using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rongmeng_20251223.Interface.Model
{
    public class CameraControlItem
    {
        public string Content { get; set; } = string.Empty;

        public IRelayCommand Command { get; set; }

        // 保存配置数据，供自动化测试流程读取
        public StationTestItem ConfigData { get; set; }
    }
}

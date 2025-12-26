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
        // 按钮显示的文字
        public string Content { get; set; } = string.Empty;

        // 按钮绑定的具体命令
        public IRelayCommand Command { get; set; }
    }
}

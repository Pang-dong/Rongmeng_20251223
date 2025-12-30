using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Rongmeng_20251223.Interface.Model
{
    public class CameraControlItem: ObservableObject
    {
        public string Content { get; set; } = string.Empty;

        public IRelayCommand Command { get; set; }

        // 保存配置数据，供自动化测试流程读取
        public StationTestItem ConfigData { get; set; }
        private int _testState = 0;

        /// <summary>
        /// 测试状态：0=默认(蓝), 1=成功(绿), 2=失败(红)
        /// </summary>
        public int TestState
        {
            get => _testState;
            set => SetProperty(ref _testState, value); // 这一句会通知界面刷新颜色
        }

    }
}

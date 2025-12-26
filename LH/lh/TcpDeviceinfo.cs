using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Rongmeng_20251223.LH
{
    public  class TcpDeviceinfo:ObservableObject
    {
        private string _ipAddress = "192.168.5.11";
        private int _port = 38326;

        /// <summary>
        /// IP地址
        /// </summary>
        public string IPAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }
    }
}

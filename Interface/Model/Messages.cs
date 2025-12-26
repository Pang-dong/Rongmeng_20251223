using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Rongmeng_20251223.Interface.Model
{
    public  class Messages:ValueChangedMessage<string>
    {
        public Messages(string value) : base(value)
        {

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Rongmeng_20251223.Interface;

namespace Rongmeng_20251223.Interface.Model
{
    // 这个信封里装的是 IDocommand 对象
    public class CommandResponseMessage : ValueChangedMessage<IDocommand>
    {
        public CommandResponseMessage(IDocommand value) : base(value)
        {
        }
    }
}

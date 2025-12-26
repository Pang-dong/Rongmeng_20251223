using Rongmeng_20251223.LH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Rongmeng_20251223.LH.MessageType;

namespace Rongmeng_20251223.Interface.Model
{
    public  class SelectFactory
    {
        public static IDocommand CreateDocomand()
        {
            return new FactoryDocommand();
        }
        public static IDocommand CreateDocomandStringAnd(MessageTypes packetType, CommandType commandType, string data)
        {
            return new FactoryDocommand( packetType,commandType,data);
        }
        public static IDocommand CreateDocomandIntArray(MessageTypes pack_type, CommandType com)
        {
            return new FactoryDocommand(pack_type, com);
        }
        public static IDocommand CreateDocomandInt(MessageTypes pack_type, CommandType com, int data)
        {
            return new FactoryDocommand(pack_type, com, data);
        }
    }
}

namespace Chat
{
    public class MsgType
    {
        public const int NameMsg = 1;
        public const int Msg = 2;
    }

    public abstract class MsgBaseCtrl
    {
        public abstract int MsgID { get; }
        public abstract void OnReceiveData(Server server, AsyncUserToken token, byte[] buff);
    }

    public class NameMsgCtrl : MsgBaseCtrl
    {
        public override int MsgID { get => MsgType.NameMsg; }
        public override void OnReceiveData(Server server, AsyncUserToken token, byte[] buff)
        {
            NamePacket namePacket = new NamePacket();
            namePacket.Read(buff);

            token.username = namePacket.nameStr;

            MsgPacket msgPacket = new MsgPacket();
            msgPacket.nameStr = "系统信息";
            msgPacket.msg = $"欢迎{namePacket.nameStr}进入聊天室";
            byte[] sendBuff = msgPacket.GenerateRequest(MsgType.Msg);
            server.SendAllMessage(sendBuff);
        }
    }

    public class MsgCtrl : MsgBaseCtrl
    {
        public override int MsgID { get => MsgType.Msg; }

        public override void OnReceiveData(Server server, AsyncUserToken token, byte[] buff)
        {
            MsgPacket msgPacket = new MsgPacket();
            msgPacket.Read(buff);

            byte[] sendBuff = msgPacket.GenerateRequest(MsgType.Msg);
            server.SendAllMessage(sendBuff);
        }
    }
}

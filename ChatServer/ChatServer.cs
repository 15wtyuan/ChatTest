using System;
using System.Net;
using System.Collections.Generic;

namespace Chat
{
    class ChatServer : Server.IListener
    {
        private Server server;

        private const int receiveBufferSize = 128 * 1024;

        private Dictionary<int, MsgBaseCtrl> handlerDict = new Dictionary<int, MsgBaseCtrl>();

        public ChatServer()
        {
            server = new Server(10, receiveBufferSize);
            server.Init();
            server.SetListener(this);

            AddHandler(new NameMsgCtrl());
            AddHandler(new MsgCtrl());
        }

        public void AddHandler(MsgBaseCtrl msgBaseCtrl)
        {
            handlerDict[msgBaseCtrl.MsgID] = msgBaseCtrl;
        }

        public void Start(string ip, int port)
        {
            IPEndPoint iPEnd = new IPEndPoint(IPAddress.Parse(ip), port);
            server.Start(iPEnd);
        }

        public void Stop()
        {
            server.Stop();
        }

        void Server.IListener.OnClientNumberChange(int num, AsyncUserToken token)
        {
            if (num > 0)
            {
                Console.WriteLine($"欢迎{token.Socket.AddressFamily}加入聊天室");
            }
            else
            {
                Console.WriteLine($"{token.Socket.AddressFamily}已经退出群聊");
            }
        }

        void Server.IListener.OnReceiveData(AsyncUserToken token, byte[] buff)
        {
            // Console.WriteLine($"{System.Text.Encoding.Default.GetString(buff)}");
            int start = 0;
            int id = PacketTools.ReadInt32(buff, ref start);

            byte[] data = new byte[buff.Length - 4];
            Array.Copy(buff, 4, data, 0, buff.Length - 4);

            if (handlerDict.ContainsKey(id))
            {
                handlerDict[id].OnReceiveData(server, token, data);
            }
        }

        void Server.IListener.stopedDel()
        {
            Console.WriteLine("服务器已经关闭");
        }
    }
}
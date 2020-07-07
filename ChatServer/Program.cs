using System;
using System.Net;

namespace Chat
{
    class Program
    {
        static void Main(string[] args)
        {
            ChatServer chatServer = new ChatServer();
            chatServer.Start("127.0.0.1", 8989);
            
            Console.WriteLine("服务器初始化完毕，输入exit结束服务");
            while (true)
            {
                string msg = Console.ReadLine();
                if (msg.Equals("exit"))
                {
                    chatServer.Stop();
                    break;
                }
            }
        }
    }
}

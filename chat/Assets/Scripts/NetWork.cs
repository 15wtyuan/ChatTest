using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;

namespace Network
{
    public enum MessageType
    {
        Connected,       //网络连接已成功建立
        DataReceived,    //接收到数据包
        Disconnected,    //网络连接已断开（可能是正常断开，也可能是由于发生错误而导致连接中断）
    }

    public class Message
    {
        public MessageType type;
        public ushort id;
        public byte[] data;
    }

    public class NetworkListener : TcpClient.IListener
    {
        private Queue<Message> messageQueue = new Queue<Message>();
        private object mqLock = new object();

        /// <summary>
        /// 获取消息队列头部的消息并从消息队列中移除此消息
        /// </summary>
        public Message GetMessage()
        {
            Message msg = null;

            lock (mqLock)
            {
                if (messageQueue.Count > 0)
                    msg = messageQueue.Dequeue();
            }
            return msg;
        }

        public void ClearMessage()
        {
            lock (mqLock)
            {
                messageQueue.Clear();
            }
        }

        public void OnConnected()
        {
            lock (mqLock)
            {
                Message msg = new Message();

                msg.type = MessageType.Connected;
                msg.data = null;
                messageQueue.Enqueue(msg);
            }
        }

        public void OnDataReceived(byte[] data, int start, int len)
        {
            lock (mqLock)
            {
                Message msg = new Message();

                msg.type = MessageType.DataReceived;
                msg.id = 0;

                msg.data = new byte[len];
                Array.Copy(data, start, msg.data, 0, len);

                messageQueue.Enqueue(msg);
            }
        }

        public void OnClosed()
        {
            lock (mqLock)
            {
                Message msg = new Message();

                msg.type = MessageType.Disconnected;
                msg.data = null;

                messageQueue.Enqueue(msg);
            }
        }
    }

    /// <summary>
    /// 本类暂时只支持在主线程中调用
    /// </summary>
    public class Client
    {
        private TcpClient tcp = null;
        private NetworkListener listener = null;

        public Client()
        {
        }

        public void Connect(string ip, int port)
        {
            Abort();

            listener = new NetworkListener();
            tcp = new TcpClient(listener);
            tcp.ConnectEx(ip, port);
        }

        public void SendPacket(byte[] data)
        {
            if (tcp != null)
            {
                tcp.SendPacket(data);
            }
        }

        public Message GetMessage()
        {
            if (listener == null)
                return null;

            return listener.GetMessage();
        }

        public void Abort()
        {
            if (tcp != null)
            {
                tcp.Abort();
                tcp = null;
            }

            if (listener != null)
            {
                listener.ClearMessage();
                listener = null;
            }
        }
    }

    /// <summary>
    /// 为简化逻辑以及降低性能消耗，此类不支持复用。
    /// 连接失败或者连接断开之后，不能够使用这个实例来重新建立连接。
    /// </summary>
    public class TcpClient
    {
        public interface IListener
        {
            void OnConnected();
            void OnClosed();
            // galen: 每一个包压入一个队列让Update线程去处理, 记得对队列加锁
            void OnDataReceived(byte[] data, int start, int len);
        }

        public const int LenSize = 4;

        protected IListener mListener = null;

        protected Socket mSocket = null;
        protected EndPoint hostEndPoint = null;

        private const int RecvBuffSize = 128 * 1024;
        private const int SendBuffSize = 16 * 1024;

        private byte[] recvBuffer = null;
        private int recv_len = 0;

        private object mSendLock; //发送锁

        private byte[] sendBuffer = null;
        private int send_len = 0;

        private bool is_sending = false;
        private object m_ValidLock = new object();
        private bool m_IsValid = true;

        public TcpClient()
        {
            mSendLock = new object();

            recvBuffer = new byte[RecvBuffSize];
            sendBuffer = new byte[SendBuffSize];
        }

        public TcpClient(IListener listener)
            : this()
        {
            mListener = listener;
        }

        private static IPAddress GetAddress(string hostNameOrAddress)
        {
            try
            {
                IPAddress[] addrList = Dns.GetHostAddresses(hostNameOrAddress);
                foreach (var addr in addrList)
                {
                    //目前只使用 IPv4 地址
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return addr;
                    }
                }
            }
            catch (System.Exception)
            {
            }

            //目前先不实现出错处理，直接返回 Loopback 地址
            return IPAddress.Loopback;
        }

        public void ConnectEx(string ip, int port)
        {
            mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            mSocket.NoDelay = true;

            IPAddress ipAddress = GetAddress(ip);
            SocketAsyncEventArgs connectArgs = new SocketAsyncEventArgs();

            hostEndPoint = new IPEndPoint(ipAddress, port);
            connectArgs.Completed += OnComplete;
            connectArgs.RemoteEndPoint = hostEndPoint;

            if (mSocket.ConnectAsync(connectArgs) == false)
            {
                ThreadPool.QueueUserWorkItem(ProcessThread, connectArgs);
            }
        }

        public void SendPacket(byte[] data)
        {
            bool ok = true;

            try
            {
                lock (mSendLock)
                {
                    if (send_len + data.Length + LenSize > sendBuffer.Length)
                        ok = false;

                    if (ok)
                    {
                        byte[] header = BitConverter.GetBytes((System.UInt32)data.Length);

                        for (int k = 0; k < LenSize; ++k)
                        {
                            sendBuffer[send_len + k] = header[k];
                        }
                        Array.Copy(data, 0, sendBuffer, send_len + LenSize, data.Length);

                        send_len += data.Length + LenSize;

                        if (is_sending == false)
                        {
                            is_sending = true;

                            SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs();

                            sendArgs.SetBuffer(sendBuffer, 0, send_len);
                            sendArgs.Completed += OnComplete;
                            if (mSocket.SendAsync(sendArgs) == false)
                            {
                                ThreadPool.QueueUserWorkItem(ProcessThread, sendArgs);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                ok = false;
            }

            if (ok == false)
                Disconnect();
        }

        /// <summary>
        /// 需要保证此函数在一个单独的线程中执行
        /// </summary>
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            lock (mSendLock)
            {
                is_sending = false;

                int len = e.BytesTransferred;

                Array.Copy(sendBuffer, len, sendBuffer, 0, send_len - len);

                send_len -= len;

                if (send_len > 0)
                {
                    is_sending = true;

                    e.SetBuffer(0, send_len);
                    if (mSocket.SendAsync(e) == false)
                    {
                        ThreadPool.QueueUserWorkItem(ProcessThread, e);
                    }
                }
            }
        }

        private void OnComplete(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Connect:
                        ProcessConnect(e);
                        break;

                    case SocketAsyncOperation.Receive:
                        ProcessReceive(e);
                        break;

                    case SocketAsyncOperation.Send:
                        ProcessSend(e);
                        break;
                }
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        private void ProcessThread(object data)
        {
            SocketAsyncEventArgs e = (SocketAsyncEventArgs)data;

            OnComplete(null, e);
        }

        private void ProcessConnect(SocketAsyncEventArgs e)
        {
            SocketError error = e.SocketError;

            if (error == SocketError.Success)
            {
                if (mListener != null)
                    mListener.OnConnected();

                SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();

                receiveArgs.SetBuffer(recvBuffer, 0, recvBuffer.Length);
                receiveArgs.Completed += OnComplete;
                if (mSocket.ReceiveAsync(receiveArgs) == false)
                {
                    ThreadPool.QueueUserWorkItem(ProcessThread, receiveArgs);
                }
            }
            else
            {
                Disconnect();
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            SocketError error = e.SocketError;

            if (error == SocketError.Success)
            {
                if (e.BytesTransferred > 0)
                {
                    recv_len += e.BytesTransferred;
                    OnSplitPacket();

                    int remainSpace = recvBuffer.Length - recv_len;

                    if (remainSpace <= 0)
                    {
                        //缓冲区空间不足
                        Disconnect();
                        return;
                    }

                    e.SetBuffer(recv_len, remainSpace);

                    if (mSocket.ReceiveAsync(e) == false)
                    {
                        ThreadPool.QueueUserWorkItem(ProcessThread, e);
                    }
                }
                else
                {
                    Disconnect();
                }
            }
            else
            {
                Disconnect();
            }
        }

        private void OnSplitPacket()
        {
            // 解包 = uint32 + content_len，并移除
            int packet_len = recv_len;
            int cursor = 0;
            int len = 0;
            while (packet_len > cursor + LenSize) //包体不能为0，所以要 >
            {
                // (1) 解析包头
                len = (int)BitConverter.ToUInt32(recvBuffer, (int)cursor);

                // 判断一下包头的内容长度是否符合限定范围, 到时候再改
                if (len > 0 && len < RecvBuffSize)
                {

                }
                else
                {
                    Disconnect();
                    break;
                }

                // (2) 解析包体
                if (packet_len >= cursor + LenSize + len) //解包成功
                {
                    cursor += LenSize;
                    if (mListener != null)
                        mListener.OnDataReceived(recvBuffer, (int)cursor, len);
                    cursor += len;
                }
                else
                {
                    break;
                }
            }

            if (cursor > 0)
            {
                Array.Copy(recvBuffer, cursor, recvBuffer, 0, recv_len - cursor);
                recv_len = (int)(recv_len - cursor);
            }
        }

        /// <summary>
        /// 调用此函数会产生一个连接关闭消息
        /// </summary>
        private void Disconnect()
        {
            lock (m_ValidLock)
            {
                if (m_IsValid)
                {
                    m_IsValid = false;

                    try
                    {
                        mSocket.Shutdown(SocketShutdown.Both);
                    }
                    catch (System.Exception)
                    {
                    }

                    mSocket.Close();

                    if (mListener != null)
                        mListener.OnClosed();
                }
            }
        }

        public void Abort()
        {
            Disconnect();
        }
    }
}

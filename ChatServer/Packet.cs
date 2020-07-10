using System;

namespace Chat
{
    public class PacketTools
    {
        #region 读取工具
        public static System.Int32 ReadInt32(byte[] data, ref int start)
        {
            System.Int32 i = System.BitConverter.ToInt32(data, start);

            start += 4;
            return i;
        }

        /// <summary>
        /// 从协议数据包中解析变长字符串
        /// </summary>
        public static string ReadString(byte[] data, ref int start)
        {
            int len = ReadInt32(data, ref start);

            if (start + len > data.Length)
                throw new Exception("Data size is too small: " + data.Length);

            string s = System.Text.Encoding.UTF8.GetString(data, start, len);

            start += len;
            return s;
        }
        #endregion

        #region 写入工具
        /// <summary>
        /// 将字符串以 UTF-8 编码的形式存入字节数组中，在写入字符串之前会先写入两个字节的长度字段，
        /// 最终写入的数据量是  4 + 字符串 UTF-8 字节数 + 1，因为字符串需要以空字符结束
        /// </summary>
        public static void WriteString(string src, byte[] dst, ref int start)
        {
            byte[] t = System.Text.Encoding.UTF8.GetBytes(src);
            UInt16 len = (UInt16)(t.Length + 1);

            //写入4个字节的长度字段
            WriteInteger(len, dst, ref start);

            t.CopyTo(dst, start);
            start += t.Length;

            dst[start] = 0;
            start++;
        }

        public static void WriteInteger(Int32 src, byte[] dst, ref int start)
        {
            byte[] t = System.BitConverter.GetBytes(src);

            t.CopyTo(dst, start);
            start += t.Length;
        }
        #endregion
        #region 其他
        public static int GetStringPacketSize(string s)
        {
            return 2 + System.Text.Encoding.UTF8.GetByteCount(s) + 1;
        }
        #endregion
    }

    public abstract class PacketBase
    {

        public byte[] GenerateRequest(int msgId)
        {
            byte[] buffer = new byte[GetPacketSize()];
            int start = 0;

            PacketTools.WriteInteger(msgId, buffer, ref start);
            WritePacket(buffer, ref start);
            return buffer;
        }

        public abstract void Read(byte[] data);

        protected abstract void WritePacket(byte[] buffer, ref int start);

        protected abstract int GetPacketSize();
    }

    public class NamePacket : PacketBase
    {

        public string nameStr;

        public override void Read(byte[] data)
        {
            int start = 0;
            nameStr = PacketTools.ReadString(data, ref start);
        }

        protected override void WritePacket(byte[] buffer, ref int start)
        {
            PacketTools.WriteString(nameStr, buffer, ref start);
        }

        protected override int GetPacketSize()
        {
            return 4 + PacketTools.GetStringPacketSize(nameStr);
        }
    }

    public class MsgPacket : PacketBase
    {
        public string nameStr;
        public string msg;

        public override void Read(byte[] data)
        {
            int start = 0;
            nameStr = PacketTools.ReadString(data, ref start);
            msg = PacketTools.ReadString(data, ref start);
        }

        protected override void WritePacket(byte[] buffer, ref int start)
        {
            PacketTools.WriteString(nameStr, buffer, ref start);
            PacketTools.WriteString(msg, buffer, ref start);
        }

        protected override int GetPacketSize()
        {
            return 4 + PacketTools.GetStringPacketSize(nameStr) + PacketTools.GetStringPacketSize(msg);
        }
    }


}
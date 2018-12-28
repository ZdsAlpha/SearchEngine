using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataStructs
{
    public class Crc32
    {
        private static uint[] Table;
        static Crc32()
        {
            uint poly = 0xEDB88320U;
            Table = new uint[256];
            uint temp = 0;
            for (uint i = 0; i <= Table.Length - 1; i++)
            {
                temp = i;
                for (int j = 8; j >= 1; j += -1)
                {
                    if ((temp & 1) == 1)
                        temp = (temp >> 1) ^ poly;
                    else
                        temp >>= 1;
                }
                Table[i] = temp;
            }
        }
        public static uint Compute(byte[] bytes)
        {
            uint crc = 0xFFFFFFFFU;
            for (int i = 0; i <= bytes.Length - 1; i++)
            {
                byte index = (byte)(((crc) & 0xFF) ^ bytes[i]);
                crc = (crc >> 8) ^ Table[index];
            }
            return ~crc;
        }
        public static uint Compute(string text, Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            return Compute(encoding.GetBytes(text));
        }
    }
}

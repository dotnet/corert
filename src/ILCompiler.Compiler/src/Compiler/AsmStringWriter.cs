using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler
{
    class AsmStringWriter
    {
        private Encoding _stringEncoding;

        private Action<byte> WriteByte { get; set; }

        public AsmStringWriter(Action<byte> byteWriter)
        {
            this.WriteByte = byteWriter;
            this._stringEncoding = UTF8Encoding.UTF8;
        }

        public void WriteUInt32(uint value)
        {
            WriteByte((byte)value);
            WriteByte((byte)(value >> 8));
            WriteByte((byte)(value >> 16));
            WriteByte((byte)(value >> 24));
        }

        public void WriteUnsigned(uint d)
        {
            if (d < 128)
            {
                WriteByte((byte)(d * 2 + 0));
            }
            else if (d < 128 * 128)
            {
                WriteByte((byte)(d * 4 + 1));
                WriteByte((byte)(d >> 6));
            }
            else if (d < 128 * 128 * 128)
            {
                WriteByte((byte)(d * 8 + 3));
                WriteByte((byte)(d >> 5));
                WriteByte((byte)(d >> 13));
            }
            else if (d < 128 * 128 * 128 * 128)
            {
                WriteByte((byte)(d * 16 + 7));
                WriteByte((byte)(d >> 4));
                WriteByte((byte)(d >> 12));
                WriteByte((byte)(d >> 20));
            }
            else
            {
                WriteByte((byte)15);
                WriteUInt32(d);
            }
        }

        public void WriteString(string s)
        {
            byte[] bytes = _stringEncoding.GetBytes(s);

            WriteUnsigned((uint)bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
                WriteByte(bytes[i]);
        }
    }
}

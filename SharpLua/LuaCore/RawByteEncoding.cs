using System;
using System.Collections.Generic;
using System.Text;

namespace SharpLua
{
    /// <summary>
    /// Implementation of Encoding that basically treats a 'char's as a zero-extended byte
    /// </summary>
    /// <remarks>
    /// This mimics the behavior of the original SharpLua code.
    /// </remarks>
    public sealed class RawByteEncoding : Encoding
    {
        public static readonly RawByteEncoding Instance = new RawByteEncoding();

        public override int GetByteCount(char[] chars, int index, int count)
        {
            return count;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            if (charCount < 0) throw new ArgumentException("charCount");
            for (int i = charIndex, j = byteIndex, cx = charCount; cx > 0; cx--, i++, j++)
            {
                bytes[j] = (byte)chars[i];
            }
            return charCount;
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            return count;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            if (byteCount < 0) throw new ArgumentException("byteCount");
            for (int i = byteIndex, j = charIndex, cx = byteCount; cx > 0; cx--, i++, j++)
            {
                chars[j] = (char)bytes[i];
            }
            return byteCount;
        }

        public override int GetMaxByteCount(int charCount)
        {
            return charCount;
        }

        public override int GetMaxCharCount(int byteCount)
        {
            return byteCount;
        }
    }
}

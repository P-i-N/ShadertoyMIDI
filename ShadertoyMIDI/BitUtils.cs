using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadertoyMIDI
{
    public static class BitUtils
    {
        public static Int16 ConvertToLittleEndian(Int16 bigEndianValue)
        {
            if (BitConverter.IsLittleEndian)
                bigEndianValue = BitConverter.ToInt16(BitConverter.GetBytes(bigEndianValue).Reverse().ToArray());

            return bigEndianValue;
        }

        public static UInt16 ConvertToLittleEndian(UInt16 bigEndianValue)
        {
            if (BitConverter.IsLittleEndian)
                bigEndianValue = BitConverter.ToUInt16(BitConverter.GetBytes(bigEndianValue).Reverse().ToArray());

            return bigEndianValue;
        }

        public static Int32 ConvertToLittleEndian(Int32 bigEndianValue)
        {
            if (BitConverter.IsLittleEndian)
                bigEndianValue = BitConverter.ToInt32(BitConverter.GetBytes(bigEndianValue).Reverse().ToArray());

            return bigEndianValue;
        }

        public static UInt32 ConvertToLittleEndian(UInt32 bigEndianValue)
        {
            if (BitConverter.IsLittleEndian)
                bigEndianValue = BitConverter.ToUInt32(BitConverter.GetBytes(bigEndianValue).Reverse().ToArray());

            return bigEndianValue;
        }
    }
}

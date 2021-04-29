using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace CampbellDAT
{
    internal static class MemoryMappedViewAccessorExtensions
    {
        private const ushort POS_INF = 0xff1f;
        private const ushort NEG_INF = 0xff9f;
        private const ushort NaN = 0xfe9f;

        // https://www.campbellsci.com/forum?forum=1&l=thread&tid=540
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float ReadFloatingPoint2(this MemoryMappedViewAccessor accessor, long offset)
        {
            var value = accessor.ReadUInt16(offset);
            var ptr = (byte*)&value;

            if (value == POS_INF)
            {
                return float.PositiveInfinity;
            }
            else if (value == NEG_INF)
            {
                return float.NegativeInfinity;
            }
            else if (value == NaN)
            {
                return float.NaN;
            }
            else
            {
                var isNegative = (ptr[0] & 0x80) > 0;
                var exponent = (ptr[0] & 0x60) >> 5;
                var mantissa = ((ptr[0] & 0x1F) << 8) + (ptr[1] << 0);
                var result = (float)(mantissa * Math.Pow(10, -exponent));

                return isNegative ? -result : result;
            }
        }

        // Campbell CR10X manual, Appendix C
        // https://s.campbellsci.com/documents/us/manuals/cr10x.pdf
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float ReadFloatingPoint4(this MemoryMappedViewAccessor accessor, long offset)
        {
#warning NaN etc detection is missing. No sample file available.
            var value = accessor.ReadUInt32(offset);
            var ptr = (byte*)&value;
            var isNegative = (ptr[0] & 0x80) > 0;
            var exponent = (ptr[0] & 0x7F) - 0x40;
            var mantissa = ((ptr[1] << 16) + (ptr[2] << 8) + (ptr[3] << 0)) / (double)0x01_00_00_00;
            var result = (float)(mantissa * Math.Pow(2, exponent));

            return isNegative ? -result : result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool ReadBool2(this MemoryMappedViewAccessor accessor, long offset)
        {
            var value = accessor.ReadUInt16(offset);
            return value != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool ReadBool4(this MemoryMappedViewAccessor accessor, long offset)
        {
            var value = accessor.ReadUInt32(offset);
            return value != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe DateTime ReadNSec(this MemoryMappedViewAccessor accessor, long offset)
        {
            var value1 = Utils.TO_UNIX_EPOCH + Utils.SwitchEndianness(accessor.ReadUInt32(offset));
            var value2 = Utils.SwitchEndianness(accessor.ReadUInt32(offset + 4));

            return DateTimeOffset
                .FromUnixTimeSeconds(value1)
                .AddMilliseconds(value2 / 1000.0 / 1000.0)
                .UtcDateTime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe DateTime ReadSecNano(this MemoryMappedViewAccessor accessor, long offset)
        {
            var value1 = Utils.TO_UNIX_EPOCH + accessor.ReadUInt32(offset);
            var value2 = accessor.ReadUInt32(offset + 4);

            return DateTimeOffset
                .FromUnixTimeSeconds(value1)
                .AddMilliseconds(value2 / 1000.0 / 1000.0)
                .UtcDateTime;
        }
    }
}

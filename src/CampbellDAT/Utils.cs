using System;
using System.Runtime.InteropServices;

namespace CampbellDAT
{
    internal static class Utils
    {
        public const int TO_UNIX_EPOCH = 631152000; // # of seconds between Unix Epoch and Campbell

        public static (int, Type) GetTypeFromCampbellType(string dataType)
        {
            // Different datatypes found in:
            // - LoggerNet Datalogger Support Software: https://s.campbellsci.com/documents/us/manuals/loggernet.pdf
            // - LoggneNet Database Software: https://s.campbellsci.com/documents/eu/manuals/lndb%20-%20878.pdf
            // - Online help: https://help.campbellsci.com/GRANITE9-10/Content/shared/Details/Data/data-types-formats.htm?TocPath=Working%20with%20data%7CData%20types%20and%20formats%7C_____0
            // - camp2ascii: https://github.com/ansell/camp2ascii
            return dataType.Split('(')[0] switch
            {
                "IEEE4"     => (4, typeof(float)),
                "IEEE4B"    => (4, typeof(float)),
                "IEEE8"     => (8, typeof(double)),
                "FP2"       => (2, typeof(float)),
                "FP4"       => (4, typeof(float)),
                "ULONG"     => (4, typeof(uint)),
                "LONG"      => (4, typeof(int)),
                "USHORT"    => (2, typeof(ushort)),
                "SHORT"     => (2, typeof(short)),
                "UINT2"     => (2, typeof(ushort)),
                "INT2"      => (2, typeof(short)),
                "UINT4"     => (4, typeof(uint)),
                "INT4"      => (4, typeof(int)),
                "BOOL"      => (1, typeof(bool)),
                "BOOL2"     => (2, typeof(bool)),
                "BOOL4"     => (4, typeof(bool)),
                "NSec"      => (4 + 4, typeof(DateTime)),
                "SecNano"   => (4 + 4, typeof(DateTime)),
                "ASCII"     => (int.Parse(dataType.Substring(6, dataType.Length - 6 - 1)), typeof(string)),
                _           => throw new Exception($"Unsupported data type '{dataType}'.")
            };

#warning More types here (CRBasic): https://help.campbellsci.com/crbasic/cr1000x/#Instructions/sample.htm?Highlight=Sample Is BOOLX correctly implemented?
        }

        public static T SwitchEndianness<T>(T value) where T : unmanaged
        {
            Span<T> data = stackalloc T[] { value };
            Utils.SwitchEndianness(data);

            return data[0];
        }

        public static void SwitchEndianness<T>(Span<T> dataset) where T : unmanaged
        {
            var size = Marshal.SizeOf<T>();
            var dataset_bytes = MemoryMarshal.Cast<T, byte>(dataset);

            for (int i = 0; i < dataset_bytes.Length; i += size)
            {
                for (int j = 0; j < size / 2; j++)
                {
                    var i1 = i + j;
                    var i2 = i - j + size - 1;

                    byte tmp = dataset_bytes[i1];
                    dataset_bytes[i1] = dataset_bytes[i2];
                    dataset_bytes[i2] = tmp;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CampbellDAT
{
    // TOB1, TOB2 or TOB3
    // https://github.com/ansell/camp2ascii
    // Loggers are programmed with CRBasic (https://help.campbellsci.com/crbasic/cr1000x/)
    // File size may be determined by (look for >>>> and <<<<):
    //
    // DataTable(name, 1, -1)
    //     TableFile("CRD:tableName", 64, 100, 0, >>>>60, Min<<<<, flag1, flag2)
    //     ...
    // EndTable
    //
    // BeginProg
    //     ' 0 = loop forever
    //     Scan(>>>>50, mSec<<<<, 1000, 0)
    //         ....
    //     NextScan
    //     ....
    // EndProg
    
    /// <summary>
    /// An in-memory representation of a Campbell TOB file.
    /// </summary>
    public class CampbellFile : IDisposable
    {
        #region Fields

        private FileStream _fileStream;
        private StreamReader _streamReader;
        private List<CampbellVariable> _variables;

        #endregion

        #region Constructors

        /// <summary>
        /// Opens and reads the Campbell DAT file header of the file specified in <paramref name="filePath"/>.
        /// </summary>
        /// <param name="filePath">The path of the file to open.</param>
        public CampbellFile(string filePath)
        {
#warning CardConvert can produce TOB1 files. The software is part of the LoggerNet software package.

            if (!BitConverter.IsLittleEndian)
                throw new Exception("This library works only on little endian systems.");

            _fileStream = File.OpenRead(filePath);

            // see spec for TOB1 or TOB2/3: "ASCII Header Line X" ... "with a carriage return and line feed (CRLF)".
            var encoding = Encoding.ASCII;
            _streamReader = new StreamReader(_fileStream, encoding);

            /* Environment: format */
            var environmentString = _streamReader.ReadLine();
            this.FirstFrameStart += encoding.GetByteCount(environmentString) + 2;

            var environment = environmentString.Split(',')
                .Select(value => value.Substring(1, value.Length - 2))
                .ToList();

            this.Format = environment[0] switch
            {
                "TOB1"  => Format.TOB1,
                "TOB2"  => Format.TOB2,
                "TOB3"  => Format.TOB3,
                _       => throw new Exception($"Unknown format '{environment[0]}'.")
            };

            if (environment.Count != 8)
                throw new Exception($"Expected 8 fields at header line 1, got {environment.Count}.");

            /* Environment: station name */
            this.StationName = environment[1];

            /* Environment: model */
            this.Model = environment[2];

            /* Environment: serial number */
            this.SerialNumber = environment[3];

            /* Environment: operating system */
            this.OperatingSystem = environment[4];

            /* Environment: program */
            this.Program = environment[5];

            /* Environment: program signature */
            this.ProgramSignature = environment[6];

            /* Environment: table name or creation time */
            if (this.Format == Format.TOB1)
                this.TableName = environment[7];
            
            else
                this.CreationTime = DateTime.ParseExact(environment[7], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            /* Environment: derived */
            (this.FrameHeaderSize, this.FrameFooterSize) = this.Format switch
            {
                Format.TOB1 => (0, 0),
                Format.TOB2 => (8, 4),
                Format.TOB3 => (12, 4),
                _           => throw new Exception($"Unknow version '{this.Format}'.")
            };

            /* Table */
            if (this.Format == Format.TOB1)
            {
                // skip table line
            }
            else
            {
                var tableString = _streamReader.ReadLine();
                this.FirstFrameStart += encoding.GetByteCount(tableString) + 2;

                var table = tableString.Split(',')
                   .Select(value => value.Substring(1, value.Length - 2))
                   .ToList();

                if (this.Format == Format.TOB2)
                {
                    if (table.Count != 6)
                        throw new Exception($"Expected 6 fields at header line 2 for TOB2, got {table.Count}.");
                }
                else if (this.Format == Format.TOB3)
                {
                    if (table.Count != 9)
                        throw new Exception($"Expected 8 fields at header line 2 for TOB3, got {table.Count}.");
                }

                /* Table: table name */
                this.TableName = table[0];

                /* Table: record interval */
                var recordIntervalParts = table[1].Split(' ');

                this.RecordInterval = double.Parse(recordIntervalParts[0]) * recordIntervalParts[1] switch
                {
                    "HOUR"  => 3600,
                    "MIN"   => 60,
                    "SEC"   => 1,
                    "MSEC"  => 1e-3,
                    "USEC"  => 1e-6,
                    "NSEC"  => 1e-9,
                    _       => throw new Exception($"Unknown sampling period unit '{recordIntervalParts[1]}'.")
                };

                /* Table: data frame size */
                this.FrameSize = int.Parse(table[2]);

                /* Table: intended table size */
                this.IntendedTableSize = int.Parse(table[3]);

                /* Table: validation stamp */
                this.ValidationStamp = int.Parse(table[4]);

                /* Table: frame time resolution */
                this.FrameTimeResolution = table[5] switch
                {
                    "SecMsec"       => 1e-3,
                    "Sec100Usec"    => 100e-6,
                    "Sec10Usec"     => 10e-6,
                    "SecUsec"       => 1e-6,
                    _               => throw new Exception($"Unknown sampling period '{table[5]}'.")
                };

                /* Table: not found in spec, but in camp2ascii */
                if (this.Format == Format.TOB3)
                {
                    this.RingRecord = int.Parse(table[6]);
                    this.LastCardRemoval = int.Parse(table[7]);
                    // meaning of the last value is unknown
                }
            }

            /* variables */
            var namesString = _streamReader.ReadLine();
            this.FirstFrameStart += encoding.GetByteCount(namesString) + 2;

            var names = namesString.Split(',')
               .Select(value => value.Substring(1, value.Length - 2))
               .ToList();

            var unitsString = _streamReader.ReadLine();
            this.FirstFrameStart += encoding.GetByteCount(unitsString) + 2;

            var units = unitsString.Split(',')
                .Select(value => value.Substring(1, value.Length - 2))
                .ToList();

            var processingTypesString = _streamReader.ReadLine();
            this.FirstFrameStart += encoding.GetByteCount(processingTypesString) + 2;

            var processingTypes = processingTypesString.Split(',')
                .Select(value => value.Substring(1, value.Length - 2))
                .ToList();

            var dataTypesString = _streamReader.ReadLine();
            this.FirstFrameStart += encoding.GetByteCount(dataTypesString) + 2;

            var dataTypes = dataTypesString.Split(',')
                .Select(value => value.Substring(1, value.Length - 2))
                .ToList();

            // remove padding
            dataTypes[dataTypes.Count - 1] = dataTypes[dataTypes.Count - 1]
                .TrimEnd()
                .TrimEnd('"');

            if (names.Count != units.Count ||
                names.Count != processingTypes.Count ||
                names.Count != dataTypes.Count)
                throw new Exception("Input file header is corrupted, number of columns is not constant.");

            _variables = names.Select((name, i)
                => new CampbellVariable(
                    name: name, 
                    unit: units[i], 
                    processingType: processingTypes[i], 
                    dataType: dataTypes[i])
                ).ToList();

            /* dimensions */
            this.FrameRowSize = this.Variables
                .Sum(variable => variable.InFileDataTypeSize);

            if (this.Format == Format.TOB1)
            {
                this.FrameSize = this.FrameRowSize;
                this.FrameRowCount = 1;
                this.FrameRowPadding = 0;
            }
            else
            {
                var framePayloadSize = this.FrameSize - this.FrameHeaderSize - this.FrameFooterSize;
                this.FrameRowCount = framePayloadSize / this.FrameRowSize;
                var totalPadding = framePayloadSize % this.FrameRowSize;

                if (totalPadding % this.FrameRowCount != 0)
                    throw new Exception("Invalid padding.");

                this.FrameRowPadding = totalPadding / this.FrameRowCount;
            }
        }

        #endregion

        #region General Properties

        /// <summary>
        /// A read-only list of variables.
        /// </summary>
        public IReadOnlyList<CampbellVariable> Variables => _variables;

        #endregion

        #region Header Properties

        /// <summary>
        /// The data file type.
        /// </summary>
        public Format Format { get; }

        /// <summary>
        /// The station name.
        /// </summary>
        public string StationName { get; }

        /// <summary>
        /// Model name of the datalogger.
        /// </summary>
        public string Model { get; }

        /// <summary>
        /// Serial number of the datalogger.
        /// </summary>
        public string SerialNumber { get; }

        /// <summary>
        /// Operating system of the datalogger.
        /// </summary>
        public string OperatingSystem { get; }

        /// <summary>
        /// Name of the program running in the datalogger.
        /// </summary>
        public string Program { get; }

        /// <summary>
        /// Signature of the program running in the datalogger.
        /// </summary>
        public string ProgramSignature { get; }

        /// <summary>
        /// The time that the file was created.
        /// </summary>
        public DateTime? CreationTime { get; }

        /// <summary>
        /// The name of the table as declared in the datalogger program.
        /// </summary>
        public string TableName { get; } = null!;

        /// <summary>
        /// The non-timestamped record interval.
        /// </summary>
        public double RecordInterval { get; }

        /// <summary>
        /// The data frame size.
        /// </summary>
        public int FrameSize { get; }

        /// <summary>
        /// The intended table size.
        /// </summary>
        public int IntendedTableSize { get; }

        /// <summary>
        /// The validation stamp.
        /// </summary>
        public int ValidationStamp { get; }

        /// <summary>
        /// The frame time resolution.
        /// </summary>
        public double FrameTimeResolution { get; }

        /// <summary>
        /// Record number written when the file was last rung.
        /// </summary>
        public int RingRecord { get; }

        /// <summary>
        /// Timestamp of last card removal.
        /// </summary>
        public int LastCardRemoval { get; }

        #endregion

        #region Derived Properties

        private int FrameHeaderSize { get; }

        private int FrameFooterSize { get; }
      
        private long FirstFrameStart { get; }

        private int FrameRowSize { get; }

        private int FrameRowCount { get; }

        private int FrameRowPadding { get; }

        #endregion

        #region "Methods"     

        /// <summary>
        /// Reads the data of the specified <paramref name="variable"/> and returns a timestamp array as well as a <seealso cref="CampbellData{T}"/> struct containing the metadata and data of the specified variable.
        /// </summary>
        /// <typeparam name="T">The generic numeric type to interpret the variable data.</typeparam>
        /// <param name="variable">The variable metadata.</param>
        public (DateTime[] TimeStamps, CampbellData<T> Data) Read<T>(CampbellVariable variable) where T : unmanaged
        {
            if (variable.DataType == typeof(string))
                throw new Exception("Variables of type 'string' cannot be read with this method.");

            var methodInfo = typeof(CampbellFile).GetMethod(nameof(this.ReadInternalUnmanaged), BindingFlags.NonPublic | BindingFlags.Instance);
            methodInfo = methodInfo.MakeGenericMethod(typeof(T), variable.DataType);
            var result = ((DateTime[], CampbellData<T>))methodInfo.Invoke(this, new object[] { variable });


            return result;
        }

        /// <summary>
        /// Reads the data of the specified <paramref name="variable"/> as string and returns a timestamp array as well as a <seealso cref="CampbellData{T}"/> struct containing the metadata and data of the specified variable.
        /// </summary>
        /// <param name="variable">The variable metadata.</param>
        public (DateTime[] TimeStamps, CampbellData<string> Data) ReadString(CampbellVariable variable)
        {
            if (variable.DataType != typeof(string))
                throw new Exception("Only variables of type 'string' may be read with this method.");

            return this.ReadInternalString(variable);
        }

#warning This is mostly duplicated code.
        private (DateTime[] TimeStamps, CampbellData<string> Data) ReadInternalString(CampbellVariable variable)
        {
            if (this.FrameRowCount > 1)
                throw new Exception("Reading multi-row frames is not yet supported.");

            var index = _variables.IndexOf(variable);

            if (index == -1)
                throw new Exception("The provided variable does not belong to this file.");

            var relativeVariableOffset = this.Variables
                .Take(index)
                .Sum(current => current.InFileDataTypeSize);

            using var mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var timestamps = new DateTime[this.IntendedTableSize];
            var buffer = new string[this.IntendedTableSize];
            var footerBuffer = new byte[4];

            /* for each frame */
            int i = 0;

            for (; i < this.IntendedTableSize; i++)
            {
                /* footer */
                if (this.Format > Format.TOB1)
                {
#warning The flags are not validated since they are not part of the specification!

                    var footerOffset = this.FirstFrameStart + this.FrameSize * i + this.FrameSize - this.FrameFooterSize;
                    accessor.ReadArray(footerOffset, footerBuffer, 0, 4);

                    var offset = ((footerBuffer[1] & 0x07) << 8) + footerBuffer[0];
                    var fileMark = (footerBuffer[1] & 0x08) > 0;
                    var cardRemoveAfterThisFrame = (footerBuffer[1] & 0x10) > 0;
                    var noRecord = (footerBuffer[1] & 0x20) > 0;
                    var isMinorFrame = (footerBuffer[1] & 0x40) > 0;
                    var validation = (footerBuffer[3] << 8) + footerBuffer[2];

                    if (this.ValidationStamp != validation)
                        break;

                    if (cardRemoveAfterThisFrame)
                        break;

                    if (noRecord)
                        throw new Exception("Reading incomplete files is not yet supported.");

                    if (isMinorFrame)
                        throw new Exception("Reading minor frames is not yet supported.");
                }

                /* header */
                var baseOffset = this.FirstFrameStart + this.FrameSize * i;

                if (this.Format > Format.TOB1)
                {
#warning "The timestamp and record number for each record are an optional output in a TOB1 file. If these elements are present, a "SECONDS", "NANOSECONDS", and "RECORD" column will be generated as names in the field list of header line two".

                    var offset = Utils.TO_UNIX_EPOCH + accessor.ReadInt32(baseOffset);
                    var subseconds = accessor.ReadInt32(baseOffset + 4);

                    timestamps[i] = DateTimeOffset
                        .FromUnixTimeSeconds(offset)
                        .AddSeconds(subseconds * this.FrameTimeResolution)
                        .UtcDateTime;

                    // check if data is in order
                    if (i > 0 && timestamps[i - 1] > timestamps[i])
                        throw new Exception("Reading unordered frames is not yet supported.");
                }

                // record number of the frame
                if (this.Format == Format.TOB3)
                {
                    var recordNumber = accessor.ReadInt32(baseOffset + 8);
                }

                /* data */
                var variableOffset = baseOffset + this.FrameHeaderSize + relativeVariableOffset;

                var variableBuffer = new byte[variable.InFileDataTypeSize];
                accessor.ReadArray(variableOffset, variableBuffer, 0, variableBuffer.Length);
                buffer[i] = Encoding.ASCII.GetString(variableBuffer).TrimEnd('\0');
            }

            if (i < this.IntendedTableSize - 1)
            {
                timestamps = timestamps.AsSpan().Slice(0, i).ToArray();
                buffer = buffer.AsSpan().Slice(0, i).ToArray();
            }

            return (timestamps, new CampbellData<string>(variable, buffer));
        }

#warning This is mostly duplicated code.
        private (DateTime[] TimeStamps, CampbellData<TOut> Data) ReadInternalUnmanaged<TOut, TVariable>(CampbellVariable variable)
             where TOut : unmanaged
             where TVariable : unmanaged
        {
            if (this.FrameRowCount > 1)
                throw new Exception("Reading multi-row frames is not yet supported.");

            var index = _variables.IndexOf(variable);

            if (index == -1)
                throw new Exception("The provided variable does not belong to this file.");

            var relativeVariableOffset = this.Variables
                .Take(index)
                .Sum(current => current.InFileDataTypeSize);

            using var mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var timestamps = new DateTime[this.IntendedTableSize];
            var byteSize = default(int);

            unsafe
            {
                byteSize = this.IntendedTableSize * sizeof(TVariable);
            }

            var result = this.GetBuffer<TOut>((ulong)byteSize);
            var buffer = MemoryMarshal.Cast<TOut, TVariable>(result.AsSpan());
            var footerBuffer = new byte[4];

            /* for each frame */
            int i = 0;

            for (; i < this.IntendedTableSize; i++)
            {
                /* footer */
                if (this.Format > Format.TOB1)
                {
#warning The flags are not validated since they are not part of the specification!

                    var footerOffset = this.FirstFrameStart + this.FrameSize * i + this.FrameSize - this.FrameFooterSize;
                    accessor.ReadArray(footerOffset, footerBuffer, 0, 4);

                    var offset = ((footerBuffer[1] & 0x07) << 8) + footerBuffer[0];
                    var fileMark = (footerBuffer[1] & 0x08) > 0;
                    var cardRemoveAfterThisFrame = (footerBuffer[1] & 0x10) > 0;
                    var noRecord = (footerBuffer[1] & 0x20) > 0;
                    var isMinorFrame = (footerBuffer[1] & 0x40) > 0;
                    var validation = (footerBuffer[3] << 8) + footerBuffer[2];

                    if (this.ValidationStamp != validation)
                        break;

                    if (cardRemoveAfterThisFrame)
                        break;

                    if (noRecord)
                        throw new Exception("Reading incomplete files is not yet supported.");

                    if (isMinorFrame)
                        throw new Exception("Reading minor frames is not yet supported.");
                }

                /* header */
                var baseOffset = this.FirstFrameStart + this.FrameSize * i;

                if (this.Format > Format.TOB1)
                {
#warning "The timestamp and record number for each record are an optional output in a TOB1 file. If these elements are present, a "SECONDS", "NANOSECONDS", and "RECORD" column will be generated as names in the field list of header line two".

                    var offset = Utils.TO_UNIX_EPOCH + accessor.ReadInt32(baseOffset);
                    var subseconds = accessor.ReadInt32(baseOffset + 4);

                    timestamps[i] = DateTimeOffset
                        .FromUnixTimeSeconds(offset)
                        .AddSeconds(subseconds * this.FrameTimeResolution)
                        .UtcDateTime;

                    // check if data is in order
                    if (i > 0 && timestamps[i - 1] > timestamps[i])
                        throw new Exception("Reading unordered frames is not yet supported.");
                }

                // record number of the frame
                if (this.Format == Format.TOB3)
                {
                    var recordNumber = accessor.ReadInt32(baseOffset + 8);
                }

                /* data */
                var variableOffset = baseOffset + this.FrameHeaderSize + relativeVariableOffset;

                switch (variable.CampbellDataType)
                {
                    case "IEEE4B":
                        buffer[i] = (TVariable)(object)Utils.SwitchEndianness(accessor.ReadSingle(variableOffset));
                        break;

                    case "FP2":
                        buffer[i] = (TVariable)(object)accessor.ReadFloatingPoint2(variableOffset);
                        break;

                    case "FP4":
                        buffer[i] = (TVariable)(object)accessor.ReadFloatingPoint4(variableOffset);
                        break;

                    case "BOOL2":
                        buffer[i] = (TVariable)(object)accessor.ReadBool2(variableOffset);
                        break;

                    case "BOOL4":
                        buffer[i] = (TVariable)(object)accessor.ReadBool4(variableOffset);
                        break;

                    case "NSec":
                        buffer[i] = (TVariable)(object)accessor.ReadNSec(variableOffset);
                        break;

                    case "SecNano":
                        buffer[i] = (TVariable)(object)accessor.ReadSecNano(variableOffset);
                        break;

                    default:
                        accessor.Read(variableOffset, out buffer[i]);
                        break;
                }
            }

            if (i < this.IntendedTableSize - 1)
            {
                timestamps = timestamps.AsSpan().Slice(0, i).ToArray();
                result = MemoryMarshal.Cast<TVariable, TOut>(buffer.Slice(0, i)).ToArray();
            }

            return (timestamps, new CampbellData<TOut>(variable, result));
        }

        /// <summary>
        /// Closes and disposes the Campbell file header and the underlying file stream.
        /// </summary>
        public void Dispose()
        {
            _streamReader?.Dispose();
        }

        private T[] GetBuffer<T>(ulong byteSize)
            where T : unmanaged
        {
            // convert file type (e.g. 2 bytes) to T (e.g. custom struct with 35 bytes)
            unsafe
            {
                var sizeOfT = (ulong)sizeof(T);

                if (byteSize % sizeOfT != 0)
                    throw new Exception("The size of the target buffer (number of selected elements times the datasets data-type byte size) must be a multiple of the byte size of the generic parameter T.");

                var arraySize = byteSize / sizeOfT;

                // create the buffer
                var result = new T[arraySize];
                return result;
            }
        }

        #endregion
    }
}

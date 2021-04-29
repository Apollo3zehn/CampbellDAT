using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using Xunit;

namespace CampbellDAT.Tests
{
    public class Tests
    {
        [Fact]
        public void CanReadHeader() 
        {
            // Arrange
            var filePath = "testdata.dat";

            // Act
            using (var campbellFile = new CampbellFile(filePath))
            {
                // Assert
                Assert.Equal(Format.TOB3, campbellFile.Format);
                Assert.Equal("ThisIsATestStation!", campbellFile.StationName);
                Assert.Equal("CR6", campbellFile.Model);
                Assert.Equal("12001", campbellFile.SerialNumber);
                Assert.Equal("CR6.Std.09.xx", campbellFile.OperatingSystem);
                Assert.Equal("CPU:MyAwesome_ProgramVer01.cr6", campbellFile.Program);
                Assert.Equal("1234", campbellFile.ProgramSignature);
                Assert.Equal(new DateTime(2020, 01, 01, 18, 00, 00, DateTimeKind.Utc), campbellFile.CreationTime);
                Assert.Equal("full_marsgarbage", campbellFile.TableName);
                Assert.Equal(0, campbellFile.RecordInterval);
                Assert.Equal(60, campbellFile.FrameSize);
                Assert.Equal(79200, campbellFile.IntendedTableSize);
                Assert.Equal(32145, campbellFile.ValidationStamp);
                Assert.Equal(0.0001, campbellFile.FrameTimeResolution);
                Assert.Equal(0, campbellFile.RingRecord);
                Assert.Equal(0, campbellFile.LastCardRemoval);
                Assert.Equal(16, campbellFile.Variables.Count);

                Assert.Equal("full_100_id", campbellFile.Variables[0].Name);
                Assert.Equal("", campbellFile.Variables[0].Unit);
                Assert.Equal("Smp", campbellFile.Variables[0].ProcessingType);
                Assert.Equal(typeof(string), campbellFile.Variables[0].DataType);

                Assert.Equal("full_100_u", campbellFile.Variables[1].Name);
                Assert.Equal("", campbellFile.Variables[1].Unit);
                Assert.Equal("Smp", campbellFile.Variables[1].ProcessingType);
                Assert.Equal(typeof(float), campbellFile.Variables[1].DataType);
            }
        }

        [Fact]
        public void CanReadData()
        {
            // Arrange
            var filePath = "testdata.dat";

            // Act
            using (var campbellFile = new CampbellFile(filePath))
            {
                var result1 = campbellFile.ReadString(campbellFile.Variables[0]);
                var result2 = campbellFile.Read<float>(campbellFile.Variables[1]);
                var result3 = campbellFile.Read<float>(campbellFile.Variables[campbellFile.Variables.Count - 2]);

                // Assert

                // channel 1 - string
                Assert.Equal(new DateTime(2021, 02, 08, 19, 00, 00, 50, DateTimeKind.Utc), result1.TimeStamps[0]);
                Assert.Equal(new DateTime(2021, 02, 08, 20, 00, 00, 00, DateTimeKind.Utc), result1.TimeStamps[72000 - 1]);
                Assert.Equal("Q", result1.Data.Buffer[0]);
                Assert.Equal("Q", result1.Data.Buffer[72000 - 1]);

                // channel 2 - floating point
                Assert.Equal(new DateTime(2021, 02, 08, 19, 00, 00, 50, DateTimeKind.Utc), result2.TimeStamps[0]);
                Assert.Equal(new DateTime(2021, 02, 08, 20, 00, 00, 00, DateTimeKind.Utc), result2.TimeStamps[72000 - 1]);
                Assert.Equal(3.113, result2.Data.Buffer[0], precision: 3);
                Assert.Equal(5.167, result2.Data.Buffer[72000 - 1], precision: 3);

                // channel x - floating point, with NaN
                Assert.Equal(new DateTime(2021, 02, 08, 19, 00, 00, 50, DateTimeKind.Utc), result3.TimeStamps[0]);
                Assert.Equal(new DateTime(2021, 02, 08, 20, 00, 00, 00, DateTimeKind.Utc), result3.TimeStamps[72000 - 1]);
                Assert.Equal(-1.16, result3.Data.Buffer[251], precision: 3);
                Assert.Equal(double.NaN, result3.Data.Buffer[252]);
                Assert.Equal(double.NaN, result3.Data.Buffer[253]);
                Assert.Equal(-1.17, result3.Data.Buffer[358], precision: 3);
            }
        }

        [Theory]
        [InlineData(0xff_1f, float.PositiveInfinity)]
        [InlineData(0xff_9f, float.NegativeInfinity)]
        [InlineData(0xfe_9f, float.NaN)]
        [InlineData(0x29_6c, 3.113)]
        public void CanDecodeFP2(ulong value, float expected)
        {
            // Arrange
            using var mmf = MemoryMappedFile.CreateNew(Guid.NewGuid().ToString(), 2, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, HandleInheritability.None);
            using var accessorWrite = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Write);
            accessorWrite.Write(0, value);

            using var accessorRead = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            // Act
            var actual = MemoryMappedViewAccessorExtensions.ReadFloatingPoint2(accessorRead, 0);

            // Assert
            Assert.Equal(expected, actual, precision: 3);
        }

        [Theory]
        [InlineData(0x49_0C_82_BF, -0.254)]
        [InlineData(0x9A_99_D9_44, 13.60)]
        public void CanDecodeFP4(ulong value, float expected)
        {
            // Arrange
            using var mmf = MemoryMappedFile.CreateNew(Guid.NewGuid().ToString(), 4, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, HandleInheritability.None);
            using var accessorWrite = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Write);
            accessorWrite.Write(0, value);

            using var accessorRead = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            // Act
            var actual = MemoryMappedViewAccessorExtensions.ReadFloatingPoint4(accessorRead, 0);

            // Assert
            Assert.Equal(expected, actual, precision: 3);
        }
    }
}
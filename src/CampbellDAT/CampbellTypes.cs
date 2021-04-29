using System;
using System.Diagnostics;

namespace CampbellDAT
{
    /// <summary>
    /// The file format.
    /// </summary>
    public enum Format
    {
        /// <summary>
        /// TOB1 files can be generated by LoggerNet when outputting data files to the PC.
        /// </summary>
        TOB1,

        /// <summary>
        /// TOB2 and TOB3 files are created when data are retrieved from external PC cards on dataloggers such as the CR9000, CR5000, and CR1000. The TOB2 file format has been replaced in new dataloggers by the TOB3 file format.
        /// </summary>
        TOB2,

        /// <summary>
        /// TOB2 and TOB3 files are created when data are retrieved from external PC cards on dataloggers such as the CR9000, CR5000, and CR1000. The TOB2 file format has been replaced in new dataloggers by the TOB3 file format.
        /// </summary>
        TOB3
    }

    /// <summary>
    /// A typed container for the actual data.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    public class CampbellData<T>
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of <seealso cref="CampbellData{T}"/> which contains metadata and data of a certain variable.
        /// </summary>
        /// <param name="variable">The variable containing the metadata.</param>
        /// <param name="buffer">The buffer containing the actual data</param>
        public CampbellData(CampbellVariable variable, T[] buffer)
        {
            this.Variable = variable;
            this.Buffer = buffer;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The variable containing the metadata.
        /// </summary>
        public CampbellVariable Variable { get; set; }

        /// <summary>
        /// The buffer containing the actual data.
        /// </summary>
        public T[] Buffer { get; set; }

        #endregion
    }

    /// <summary>
    /// Represents a variable within a Campbell DAT file.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class CampbellVariable
    {
        internal CampbellVariable(string name, string unit, string processingType, string dataType)
        {
            this.Name = name;
            this.Unit = unit;
            this.ProcessingType = processingType;
            this.CampbellDataType = dataType;

            (this.InFileDataTypeSize, this.DataType) = Utils.GetTypeFromCampbellType(dataType);
        }

        /// <summary>
        /// Gets the name of the variable.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the unit of the variable.
        /// </summary>
        public string Unit { get; }

        /// <summary>
        /// Gets the data type of the variable.
        /// </summary>
        public Type DataType { get; }

        /// <summary>
        /// Gets the processing performed in the datalogger to produce the value for each field in the record; for example, sample, average, min, max, etc.
        /// </summary>
        public string ProcessingType { get; }

        internal string CampbellDataType { get; }

        internal int InFileDataTypeSize { get; }
    }
}

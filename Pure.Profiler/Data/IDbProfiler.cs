

using System;
using System.Data;
using Pure.Profiler.Timings;

namespace Pure.Profiler.Data
{
    /// <summary>
    /// Represents a generic DB profiler for profiling execution of <see cref="IDbCommand"/>.
    /// </summary>
    public interface IDbProfiler
    {
        /// <summary>
        /// Executes &amp; profiles the execution of the specified <see cref="IDbCommand"/>.
        /// </summary>
        /// <param name="executeType">The <see cref="DbExecuteType"/>.</param>
        /// <param name="command">The <see cref="IDbCommand"/> to be executed &amp; profiled.</param>
        /// <param name="execute">
        ///     The execute handler, 
        ///     which should return the <see cref="IDataReader"/> instance if it is an ExecuteReader operation.
        ///     If it is not ExecuteReader, it should return null.
        /// </param>
        /// <param name="tags">The tags of the <see cref="DbTiming"/> which will be created internally.</param>
        IDataReader ExecuteDbCommand(DbExecuteType executeType, IDbCommand command, Func<object> execute, TagCollection tags);

        /// <summary>
        /// Notifies the profiler that the data reader has finished reading
        /// so that the DB timing attached to the data reading could be stopped.
        /// </summary>
        /// <param name="dataReader">The <see cref="IDataReader"/>.</param>
        void DataReaderFinished(IDataReader dataReader);
    }
}

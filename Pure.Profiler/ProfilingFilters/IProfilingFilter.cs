

using System.Collections.Generic;

namespace Pure.Profiler.ProfilingFilters
{
    /// <summary>
    /// Represents a filter to filter profiling sessions from being started by name and tags.
    /// </summary>
    public interface IProfilingFilter
    {
        string Name { get; }
        string Args { get; }
        /// <summary>
        /// Returns whether or not the profiling session should NOT be started.
        /// </summary>
        /// <param name="name">The name of the profiling session to be started.</param>
        /// <param name="tags">The tags of the profiling session to be started.</param>
        /// <returns>Returns true, if the profiling session should NOT be started, otherwise, returns false.</returns>
        bool ShouldBeExculded(string name, IEnumerable<string> tags);
    }
}

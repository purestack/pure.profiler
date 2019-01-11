

using System;
using System.Collections.Generic;

namespace Pure.Profiler.ProfilingFilters
{
    /// <summary>
    /// An <see cref="IProfilingFilter"/> implementation for ignoring profiling is name contains specified substr.
    /// </summary>
    public sealed class NameContainsProfilingFilter : IProfilingFilter
    {
        private readonly string _substr;
        public string Name
        {
            get { return "NameContainsProfilingFilter"; }
        }

        public string Args
        {
            get { return SubString; }
        }
        /// <summary>
        /// The sub string to be checked for contains.
        /// </summary>
        public string SubString { get { return _substr; } }

        #region Constructors

        /// <summary>
        /// Initializes a <see cref="NameContainsProfilingFilter"/>.
        /// </summary>
        /// <param name="substr">The substr to check contains.</param>
        public NameContainsProfilingFilter(string substr)
        {
            _substr = substr;
        }

        #endregion

        #region IProfilingFilter Members

        bool IProfilingFilter.ShouldBeExculded(string name, IEnumerable<string> tags)
        {
            if (name == null)
            {
                return true;
            }

            return name.IndexOf(_substr, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pure.Profiler.Timings;
using Newtonsoft.Json.Linq;

namespace Pure.Profiler.Web.Import.LogParsers
{
    /// <summary>
    /// Loads profiling sessions from log4net log files.
    /// </summary>
    public sealed class FileProfilingLogParser : ProfilingLogParserBase
    {
        private readonly string[] _logFileLines;

        #region Constructors

        /// <summary>
        /// Initializes a <see cref="FileProfilingLogParser"/>.
        /// </summary>
        /// <param name="logFileName"></param>
        public FileProfilingLogParser(string logFileName)
        {
            if (string.IsNullOrEmpty(logFileName))
            {
                throw new ArgumentNullException("logFileName");
            }

            if (!File.Exists(logFileName))
            {
                throw  new ArgumentException("Log file doesn't exist: " + logFileName);
            }

            using (var inStream = new FileStream(logFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var sr = new StreamReader(inStream);
                var lines = new List<string>();
                while (!sr.EndOfStream) lines.Add(sr.ReadLine());
                _logFileLines = lines.ToArray();
            }
        }

        #endregion

        #region ProfilingLogParserBase Members

        /// <summary>
        /// Loads latest top profiling session summaries from log.
        /// </summary>
        /// <param name="top"></param>
        /// <param name="minDuration"></param>
        /// <returns></returns>
        public override IEnumerable<ITimingSession> LoadLatestSessionSummaries(uint? top = 100, uint? minDuration = 0)
        {
            var results = new List<ITimingSession>();

            for (var i = _logFileLines.Length - 1; i >= 0; --i)
            {
                var sessionJson = JObject.Parse(_logFileLines[i]);
                if (sessionJson["type"].ToObject<string>() == "session" && sessionJson["duration"].ToObject<long>() >= minDuration.GetValueOrDefault())
                {
                    var session = ParseSessionFields(sessionJson);
                    results.Add(session);

                    if (results.Count >= top.GetValueOrDefault(100))
                    {
                        break;
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Loads a full profiling session from log.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public override ITimingSession LoadSession(Guid sessionId)
        {
            var jsonArray = new List<JObject>();

            // parse json array of specified session
            for (var i = _logFileLines.Length - 1; i >= 0; --i)
            {
                var json = JObject.Parse(_logFileLines[i]);
                if (json["sessionId"].ToObject<Guid>() == sessionId)
                {
                    jsonArray.Add(json);
                }
            }

            if (jsonArray.Count == 0) return null;

            // parse session
            var sessionJson = jsonArray.FirstOrDefault(json => json["type"].ToObject<string>() == "session");
            if (sessionJson == null) return null;

            var session = ParseSessionFields(sessionJson);
            var timings = new List<ITiming>();

            // parse timings
            var timingJsons = jsonArray.Where(json => json["type"].ToObject<string>() != "session");
            foreach (var timingJson in timingJsons)
            {
                var timing = ParseTimingFields(timingJson);
                timings.Add(timing);
            }

            session.Timings = SortSessionTimings(timings);

            return session;
        }

        /// <summary>
        /// Drill down profiling session from log.
        /// </summary>
        /// <param name="correlationId"></param>
        /// <returns></returns>
        public override ITimingSession DrillDownSession(Guid correlationId)
        {
            for (var i = _logFileLines.Length - 1; i >= 0; --i)
            {
                var json = JObject.Parse(_logFileLines[i]);
                JToken id;
                if (json["type"].ToObject<string>() == "session" && json.TryGetValue("correlationId", out id) && id.ToObject<Guid>() == correlationId)
                {
                    return LoadSession(json["sessionId"].ToObject<Guid>());
                }
            }

            return null;
        }

        /// <summary>
        /// Drill up profiling session from log.
        /// </summary>
        /// <param name="correlationId"></param>
        /// <returns></returns>
        public override ITimingSession DrillUpSession(Guid correlationId)
        {
            for (var i = _logFileLines.Length - 1; i >= 0; --i)
            {
                var json = JObject.Parse(_logFileLines[i]);
                JToken id;
                if (json["type"].ToObject<string>() != "session" && json.TryGetValue("correlationId", out id) && id.ToObject<Guid>() == correlationId)
                {
                    return LoadSession(json["sessionId"].ToObject<Guid>());
                }
            }

            return null;
        }

        #endregion
    }
}



using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Pure.Profiler.Timings;
using Newtonsoft.Json.Linq;

namespace Pure.Profiler.Web.Import.LogParsers
{
    /// <summary>
    /// Loads profiling sessions from Elasticsearch.
    /// </summary>
    public sealed class ElasticsearchProfilingLogParser : ProfilingLogParserBase
    {
        private readonly Uri _searchPath;

        /// <summary>
        /// Data fields should be ignored on parsing timing.
        /// </summary>
        public string[] IgnoreDataFieldNames = new [] { "@timestamp", "@version" };

        #region Constructors

        /// <summary>
        /// Initializes a <see cref="ElasticsearchProfilingLogParser"/>.
        /// </summary>
        /// <param name="searchPath"></param>
        public ElasticsearchProfilingLogParser(Uri searchPath)
        {
            if (searchPath == null)
            {
                throw new ArgumentNullException("searchPath");
            }

            _searchPath = searchPath;
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
            var sessions = new List<ITimingSession>();

            var request = WebRequest.Create(_searchPath) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/json";
            request.KeepAlive = true;
            request.UseDefaultCredentials = true;
            request.UserAgent = "ElasticsearchProfilingLogParser";
            request.AllowAutoRedirect = false;
            var postData = CreateQueryLatestJson(top.GetValueOrDefault(100), minDuration.GetValueOrDefault(0));
            var postBytes = Encoding.UTF8.GetBytes(postData);
            request.ContentLength = postBytes.Length;

            using (var postStream = request.GetRequestStream())
            {
                postStream.Write(postBytes, 0, postBytes.Length);
                postStream.Flush();
            }

            using (var response = request.GetResponse() as HttpWebResponse)
            using (var stream = new StreamReader(response.GetResponseStream()))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var content = stream.ReadToEnd();
                    var json = JObject.Parse(content);

                    var hitsJson = json["hits"]["hits"].Children();
                    if (!hitsJson.Any())
                    {
                        return null;
                    }

                    var hasSession = hitsJson.Select(hit => hit["_source"]).Any(source => source["type"].ToObject<string>() == "session");
                    if (!hasSession)
                    {
                        return null;
                    }

                    // parse session
                    var sessionJsons =
                        hitsJson.Select(hit => hit["_source"]).Where(source => source["type"].ToObject<string>() == "session");
                    sessions.AddRange(sessionJsons.OfType<JObject>().Select(ParseSessionFields));
                }
            }

            return sessions;
        }

        /// <summary>
        /// Loads a full profiling session from log.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public override ITimingSession LoadSession(Guid sessionId)
        {
            var request = WebRequest.Create(_searchPath) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/json";
            request.KeepAlive = true;
            request.UseDefaultCredentials = true;
            request.UserAgent = "ElasticsearchProfilingLogParser";
            request.AllowAutoRedirect = false;
            var postData = CreateQueryBySessionIdJson(sessionId);
            var postBytes = Encoding.UTF8.GetBytes(postData);
            request.ContentLength = postBytes.Length;

            using (var postStream = request.GetRequestStream())
            {
                postStream.Write(postBytes, 0, postBytes.Length);
                postStream.Flush();
            }

            using (var response = request.GetResponse() as HttpWebResponse)
            using (var stream = new StreamReader(response.GetResponseStream()))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var content = stream.ReadToEnd();
                    var json = JObject.Parse(content);

                    var hitsJson = json["hits"]["hits"].Children();
                    if (!hitsJson.Any())
                    {
                        return null;
                    }

                    var hasSession = hitsJson.Select(hit => hit["_source"]).Any(source => source["type"].ToObject<string>() == "session");
                    if (!hasSession)
                    {
                        return null;
                    }

                    // parse session
                    var sessionJson = hitsJson.Select(hit => hit["_source"]).First(source => source["type"].ToObject<string>() == "session");
                    var session = ParseSessionFields((JObject)sessionJson);
                    var timings = new List<ITiming>();

                    // parse timings
                    var timingJsons = hitsJson.Select(hit => hit["_source"]).Where(source => source["type"].ToObject<string>() != "session");
                    foreach (var timingJson in timingJsons)
                    {
                        var timing = ParseTimingFields((JObject)timingJson);
                        timings.Add(timing);
                    }

                    session.Timings = SortSessionTimings(timings);

                    return session;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether or not this data field should be ignored.
        /// </summary>
        /// <param name="timing"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        protected override bool IsIgnoreDataField(ITiming timing, string key)
        {
            if (IgnoreDataFieldNames != null && IgnoreDataFieldNames.Any(f => string.Equals(f, key)))
            {
                return true;
            }

            return base.IsIgnoreDataField(timing, key);
        }

        /// <summary>
        /// Drill down profiling session from log.
        /// </summary>
        /// <param name="correlationId"></param>
        /// <returns></returns>
        public override ITimingSession DrillDownSession(Guid correlationId)
        {
            return DrillSession(correlationId, true);
        }

        /// <summary>
        /// Drill up profiling session from log.
        /// </summary>
        /// <param name="correlationId"></param>
        /// <returns></returns>
        public override ITimingSession DrillUpSession(Guid correlationId)
        {
            return DrillSession(correlationId, false);
        }

        #endregion

        #region Private Methods

        private string CreateQueryBySessionIdJson(Guid sessionId)
        {
            return "{\"query\":{\"filtered\":{\"query\":{\"bool\":{\"should\":[{\"query_string\":{\"query\":\"sessionId:" + sessionId.ToString("N") + "\"}}]}}}},\"size\":1000}";
        }

        private string CreateQueryByCorrelationIdJson(Guid correlationId, bool isSession)
        {
            return "{\"query\":{\"filtered\":{\"query\":{\"bool\":{\"should\":[{\"query_string\":{\"query\":\"correlationId:" + correlationId.ToString("N") + "\"}}]}},\"filter\":{\"bool\":{\"must" + (isSession ? "" : "_not") + "\":[{\"fquery\":{\"query\":{\"query_string\":{\"query\":\"type:session\"}},\"_cache\": true}}]}}}},\"size\":1}";
        }

        private string CreateQueryLatestJson(uint top, uint minDuration)
        {
            return "{\"query\":{\"filtered\":{\"query\":{\"bool\":{\"should\":[{\"query_string\":{\"query\":\"type:session\"}}]}},\"filter\":{\"bool\":{\"must\":[{\"fquery\":{\"query\":{\"query_string\":{\"query\":\"duration:[" + minDuration + " TO *]\"}},\"_cache\": true}}]}}}},\"size\":" + top + ", \"sort\":[{\"@timestamp\":{\"order\": \"desc\",\"ignore_unmapped\": true}}]}";
        }

        private ITimingSession DrillSession(Guid correlationId, bool isDrillDown)
        {
            var request = WebRequest.Create(_searchPath) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/json";
            request.KeepAlive = true;
            request.UseDefaultCredentials = true;
            request.UserAgent = "ElasticsearchProfilingLogParser";
            request.AllowAutoRedirect = false;
            var postData = CreateQueryByCorrelationIdJson(correlationId, isDrillDown);
            var postBytes = Encoding.UTF8.GetBytes(postData);
            request.ContentLength = postBytes.Length;

            using (var postStream = request.GetRequestStream())
            {
                postStream.Write(postBytes, 0, postBytes.Length);
                postStream.Flush();
            }

            using (var response = request.GetResponse() as HttpWebResponse)
            using (var stream = new StreamReader(response.GetResponseStream()))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var content = stream.ReadToEnd();
                    var json = JObject.Parse(content);

                    var hitsJson = json["hits"]["hits"].Children();
                    if (!hitsJson.Any())
                    {
                        return null;
                    }

                    var sessionJson = hitsJson.Select(hit => hit["_source"]).First();
                    return LoadSession(sessionJson["sessionId"].ToObject<Guid>());
                }
            }

            return null;
        }

        #endregion
    }
}

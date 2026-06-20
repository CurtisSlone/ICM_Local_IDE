// Minimal Ollama client. Port of ollama.rs.
//
// DEVIATION FROM THE RUST PORT, ON PURPOSE: the Rust client hand-rolls HTTP over std TcpStream
// (build the request, read to EOF, de-chunk by hand) specifically to avoid pulling in an HTTP
// crate. On .NET the platform's HTTP client, HttpWebRequest, lives in System.dll (always
// present, no extra package), so using it IS the "lean, use the standard library" move here. It
// also handles Transfer-Encoding: chunked and the response framing for us, removing the manual
// de-chunk loop that was a bug surface in the Rust version.
//
// Two DEVLOG lessons are preserved structurally: a READ TIMEOUT (Timeout + ReadWriteTimeout, so
// a stalled Ollama returns an error instead of hanging forever) and the grammar-constrained
// `format` field (the model is a proposer; constrain its shape so the oracle has less to reject).

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Icm
{
    // A cancellation handle for an in-flight request. The GUI's Cancel button calls Abort(),
    // which aborts the underlying HttpWebRequest so a blocking GetResponse() returns at once.
    internal sealed class Cancel
    {
        private readonly object gate = new object();
        private HttpWebRequest req;
        private volatile bool cancelled;

        public bool Cancelled { get { return cancelled; } }

        public void Register(HttpWebRequest r)
        {
            lock (gate)
            {
                req = r;
                if (cancelled && r != null) { try { r.Abort(); } catch { } }
            }
        }

        public void Done() { lock (gate) { req = null; } }

        public void Abort()
        {
            cancelled = true;
            lock (gate) { if (req != null) { try { req.Abort(); } catch { } } }
        }
    }

    // Process-wide tally of local-model token usage, so the console can show the operator how much
    // work the LOCAL model did (and therefore did NOT cost in frontier tokens). Ollama reports
    // prompt_eval_count (tokens read) and eval_count (tokens generated) on each /api/generate reply.
    internal static class TokenMeter
    {
        public static long Prompt;
        public static long Eval;
        public static int Calls;
        public static void Record(long prompt, long eval) { Prompt += prompt; Eval += eval; Calls++; }
        public static long Total { get { return Prompt + Eval; } }
    }

    internal static class Ollama
    {
        private const int NumCtx = 8192; // Ollama default 2048 silently truncates long prompts.

        private static long LongOf(Dictionary<string, object> o, string key)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v)) return 0;
            double? d = Json.ToDouble(v);
            return d.HasValue ? (long)d.Value : 0;
        }

        private static string Send(string url, string method, string path, string body, int timeoutMs, Cancel cancel)
        {
            string full = url.TrimEnd('/') + path;
            HttpWebRequest req;
            try { req = (HttpWebRequest)WebRequest.Create(full); }
            catch (Exception e) { throw new IcmError("bad Ollama url '" + url + "': " + e.Message); }

            req.Method = method;
            req.KeepAlive = false;               // mirror the Rust `Connection: close`
            req.Timeout = timeoutMs;             // wait for the response to begin
            req.ReadWriteTimeout = timeoutMs;    // the read-timeout lesson: no infinite hang
            req.Proxy = null;                    // localhost; skip proxy auto-detect latency

            try
            {
                if (cancel != null) cancel.Register(req);
                if (body != null)
                {
                    req.ContentType = "application/json";
                    byte[] data = Encoding.UTF8.GetBytes(body);
                    req.ContentLength = data.Length;
                    using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                }
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    if (resp.StatusCode != HttpStatusCode.OK)
                        throw new IcmError("Ollama returned non-200 status: " + (int)resp.StatusCode);
                    using (Stream rs = resp.GetResponseStream())
                    using (var sr = new StreamReader(rs, Encoding.UTF8))
                        return sr.ReadToEnd();
                }
            }
            catch (WebException we)
            {
                if (cancel != null && cancel.Cancelled) throw new IcmError("request cancelled");
                // Surface the server's body on an HTTP error; otherwise the transport message.
                var er = we.Response as HttpWebResponse;
                if (er != null)
                {
                    string detail;
                    try { using (var sr = new StreamReader(er.GetResponseStream())) detail = sr.ReadToEnd(); }
                    catch { detail = ""; }
                    throw new IcmError("Ollama at " + full + " returned " + (int)er.StatusCode + ": " + detail);
                }
                throw new IcmError("contacting Ollama at " + full + " (timeout " + timeoutMs + "ms?): " + we.Message);
            }
            finally
            {
                if (cancel != null) cancel.Done();
            }
        }

        // One /api/generate call. With `format`, the output is grammar-constrained to that JSON
        // schema (the model cannot emit off-schema). Returns the raw `response` string, trimmed.
        public static string Generate(string url, string model, string prompt, object format,
                                      double temperature, int timeoutMs, Cancel cancel = null)
        {
            var options = new Dictionary<string, object>();
            options["num_ctx"] = NumCtx;
            options["temperature"] = temperature;
            var body = new Dictionary<string, object>();
            body["model"] = model;
            body["prompt"] = prompt;
            body["stream"] = false;
            body["options"] = options;
            if (format != null) body["format"] = format;

            string raw = Send(url, "POST", "/api/generate", Json.Serialize(body), timeoutMs, cancel);
            Dictionary<string, object> parsed;
            try { parsed = Json.AsObject(Json.Parse(raw)); }
            catch (Exception e) { throw new IcmError("parsing Ollama JSON: " + e.Message); }
            string response = Json.GetString(parsed, "response");
            if (response == null)
                throw new IcmError("no 'response' field in Ollama reply: " + raw);
            TokenMeter.Record(LongOf(parsed, "prompt_eval_count"), LongOf(parsed, "eval_count"));
            return response.Trim();
        }

        // Streaming /api/generate (freeform only - no `format`). Reads the NDJSON token stream, calls
        // onToken for each piece as it arrives, and returns the full text. Mirrors Send's request setup
        // and error handling, but reads incrementally instead of to EOF.
        public static string GenerateStream(string url, string model, string prompt, double temperature,
                                            int timeoutMs, Action<string> onToken, Cancel cancel = null)
        {
            var options = new Dictionary<string, object>();
            options["num_ctx"] = NumCtx;
            options["temperature"] = temperature;
            var body = new Dictionary<string, object>();
            body["model"] = model;
            body["prompt"] = prompt;
            body["stream"] = true;
            body["options"] = options;

            string full = url.TrimEnd('/') + "/api/generate";
            HttpWebRequest req;
            try { req = (HttpWebRequest)WebRequest.Create(full); }
            catch (Exception e) { throw new IcmError("bad Ollama url '" + url + "': " + e.Message); }
            req.Method = "POST";
            req.KeepAlive = false;
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;    // per-read: a stall between tokens errors, never hangs
            req.Proxy = null;

            var sb = new StringBuilder();
            try
            {
                if (cancel != null) cancel.Register(req);
                req.ContentType = "application/json";
                byte[] data = Encoding.UTF8.GetBytes(Json.Serialize(body));
                req.ContentLength = data.Length;
                using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    if (resp.StatusCode != HttpStatusCode.OK)
                        throw new IcmError("Ollama returned non-200 status: " + (int)resp.StatusCode);
                    using (Stream rs = resp.GetResponseStream())
                    using (var sr = new StreamReader(rs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)   // one JSON object per line (NDJSON)
                        {
                            line = line.Trim();
                            if (line.Length == 0) continue;
                            Dictionary<string, object> obj;
                            try { obj = Json.AsObject(Json.Parse(line)); } catch { continue; }
                            if (obj == null) continue;
                            string piece = Json.GetString(obj, "response");
                            if (!string.IsNullOrEmpty(piece)) { sb.Append(piece); if (onToken != null) onToken(piece); }
                            if (Json.GetBool(obj, "done", false))
                            {
                                TokenMeter.Record(LongOf(obj, "prompt_eval_count"), LongOf(obj, "eval_count"));
                                break;
                            }
                        }
                    }
                }
            }
            catch (WebException we)
            {
                if (cancel != null && cancel.Cancelled) throw new IcmError("request cancelled");
                var er = we.Response as HttpWebResponse;
                if (er != null)
                {
                    string detail;
                    try { using (var srr = new StreamReader(er.GetResponseStream())) detail = srr.ReadToEnd(); }
                    catch { detail = ""; }
                    throw new IcmError("Ollama at " + full + " returned " + (int)er.StatusCode + ": " + detail);
                }
                throw new IcmError("contacting Ollama at " + full + " (timeout " + timeoutMs + "ms?): " + we.Message);
            }
            finally { if (cancel != null) cancel.Done(); }

            return sb.ToString().Trim();
        }

        // Convenience for schema-constrained calls: parse the response string as a JSON object.
        public static Dictionary<string, object> GenerateJson(string url, string model, string prompt,
                                                              object schema, double temperature, int timeoutMs, Cancel cancel = null)
        {
            string text = Generate(url, model, prompt, schema, temperature, timeoutMs, cancel);
            try
            {
                Dictionary<string, object> obj = Json.AsObject(Json.Parse(text));
                if (obj == null) throw new IcmError("not a JSON object");
                return obj;
            }
            catch (IcmError) { throw new IcmError("model returned non-JSON under schema:\n" + text); }
            catch (Exception e) { throw new IcmError("model returned non-JSON under schema: " + e.Message + "\n" + text); }
        }

        // List installed models (/api/tags); also the cheapest reachability check.
        public static List<string> Tags(string url)
        {
            string raw = Send(url, "GET", "/api/tags", null, 5000, null);
            var names = new List<string>();
            Dictionary<string, object> parsed;
            try { parsed = Json.AsObject(Json.Parse(raw)); }
            catch (Exception e) { throw new IcmError("parsing /api/tags JSON: " + e.Message); }
            foreach (object m in Json.GetArr(parsed, "models"))
            {
                var mo = m as Dictionary<string, object>;
                string name = Json.GetString(mo, "name");
                if (name != null) names.Add(name);
            }
            return names;
        }

        // One /api/embed call: returns the embedding vector for `text` (the embedder seat).
        public static double[] Embed(string url, string model, string text, Cancel cancel = null)
        {
            var body = new Dictionary<string, object>();
            body["model"] = model;
            body["input"] = text;
            string raw = Send(url, "POST", "/api/embed", Json.Serialize(body), 30000, cancel);
            Dictionary<string, object> parsed = Json.AsObject(Json.Parse(raw));
            if (parsed == null) throw new IcmError("no JSON from /api/embed");
            List<object> embs = Json.GetArr(parsed, "embeddings");      // {"embeddings":[[...]]}
            if (embs.Count > 0) return ToDoubleArray(Json.AsArr(embs[0]));
            object single;
            if (parsed.TryGetValue("embedding", out single)) return ToDoubleArray(Json.AsArr(single)); // older shape
            throw new IcmError("no 'embeddings' field in /api/embed reply");
        }

        private static double[] ToDoubleArray(List<object> nums)
        {
            var arr = new double[nums.Count];
            for (int i = 0; i < nums.Count; i++) { double? d = Json.ToDouble(nums[i]); arr[i] = d.HasValue ? d.Value : 0.0; }
            return arr;
        }
    }
}

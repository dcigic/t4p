using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace c1g1c
{
    class t4
    {
        const string scopeRegex = @"(\$\(.*\))"; // $( exp )
        static void Main(string[] args)
        {
            var stopWatch = Stopwatch.StartNew();

            // find script maches and shedule evaluation
            var template = File.ReadAllText("template");
            Func<string, string> strip = str => str.Remove(str.LastIndexOf(')'), 1).TrimStart('$', '(');
            var maches = Regex.Matches(template, scopeRegex).Select((m) => (match: m, eval: CSharpScript.EvaluateAsync(strip(m.Value), globals: new Globals())));

            // wait until evaluations are done and combine matches and evaluations results
            Task.WaitAll(maches.Select((m) => m.eval).ToArray());
            var matchResults = maches.Select(x => (match: x.match, evalResult: x.eval.Result));

            // go through maches and replace it with eval results
            var outputBuffer = new OutputBuffer(template, matchResults);

            System.Console.WriteLine(outputBuffer.ToString());
            System.Console.WriteLine(TimeSpan.FromMilliseconds(stopWatch.ElapsedMilliseconds).Seconds);
            stopWatch.Stop();
        }

        public class OutputBuffer
        {
            byte[] _source;
            byte[] _output;
            Encoding DefaultEncoding;
            IEnumerable<(int, int, byte[])> _hits;
            public OutputBuffer(string src, IEnumerable<(Match, object)> enm, Encoding enc = null)
            : this(src, enm.Select(e => (e.Item1.Index, e.Item1.Length, e.Item2.ToString())))
            {
            }

            public OutputBuffer(string src, IEnumerable<(int, int, string)> hits, Encoding enc = null)
            {
                DefaultEncoding = enc ?? Encoding.UTF8;
                _source = DefaultEncoding.GetBytes(src);
                _hits = hits.Select(h => (h.Item1, h.Item2, DefaultEncoding.GetBytes(h.Item3)));
            }

            private bool _built;

            public override string ToString()
            {
                if (!_built)
                    Build();
                return DefaultEncoding.GetString(_output);
            }

            void Build()
            {
                // calculate new output length
                var evalResultsLength = _hits.Sum(a => a.Item3.Length);
                var evalsLength = _hits.Sum(a => a.Item2);
                var newOutputLength = (_source.Length - evalsLength) + evalResultsLength;
                _output = new byte[newOutputLength];

                int srcPtr = 0;
                int outPtr = 0;
                var e = _hits.GetEnumerator();
                while (e.MoveNext())
                {
                    (int from, int length, byte[] evalResult) = e.Current;

                    Array.Copy(_source, srcPtr, _output, outPtr, from - srcPtr);
                    outPtr += from - srcPtr;

                    Array.Copy(evalResult, 0, _output, outPtr, evalResult.Length);
                    outPtr += evalResult.Length;

                    srcPtr = from + length;
                }

                // Wrap it up
                Array.Copy(_source, srcPtr, _output, outPtr, _source.Length - srcPtr);
                _built = true;
            }



        }

    }


}

public class Globals
{
    public string Write(object obj)
    {
        return obj.ToString();
    }
}

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
            var matchResults = maches.Select(x => (match: x.match, evalResult: x.eval.Result.ToString().ToCharArray()));

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
            public Encoding DefaultEncoding = Encoding.UTF8;
            IEnumerable<(Match, char[])> _enm;

            public OutputBuffer(string src, IEnumerable<(Match, char[])> enm)
            {
                _source = Encoding.UTF8.GetBytes(src);
                _enm = enm;
            }

            long _srcPtr;
            long _outPtr;
            private bool _built;

            void Add(Match match, char[] evalResult)
            {
                var evalBytes = DefaultEncoding.GetBytes(evalResult);
                Array.Copy(_source, _srcPtr, _output, _outPtr, match.Index - _srcPtr);
                _outPtr += match.Index - _srcPtr;

                Array.Copy(evalBytes, 0, _output, _outPtr, evalBytes.Length);
                _outPtr += evalBytes.Length;

                _srcPtr = match.Index + match.Value.Length;
            }

            public override string ToString()
            {
                if (!_built)
                    Build();
                return DefaultEncoding.GetString(_output);
            }

            void Build()
            {
                // calculate new output length
                var evalResultsLength = _enm.Sum(a => a.Item2.Length);
                var evalsLength = _enm.Sum(a => a.Item1.Value.Length);
                var newOutputLength = (_source.Length - evalsLength) + evalResultsLength;
                _output = new byte[newOutputLength];

                var e = _enm.GetEnumerator();
                while (e.MoveNext())
                {
                    (Match match, char[] taskResult) = e.Current;
                    Add(match, taskResult);
                }

                // Wrap it up
                Array.Copy(_source, _srcPtr, _output, _outPtr, _source.Length - _srcPtr);
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

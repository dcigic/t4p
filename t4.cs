using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace t4
{
    class Program
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

            // calculate new output length
            var evalResultsLength = matchResults.Sum(a => a.evalResult.Length);
            var evalsLength = matchResults.Sum(a => a.match.Value.Length);
            var newOutputLength = (template.Length - evalsLength) + evalResultsLength;

            // go through maches and replace it with eval results
            var outputBuffer = new OutputBuffer(template, newOutputLength);

            var enm = matchResults.GetEnumerator();
            while (enm.MoveNext())
            {
                (Match match, char[] taskResult) = enm.Current;
                outputBuffer.Add(match, taskResult);
            }

            outputBuffer.WrapUp();

            stopWatch.Stop();
            System.Console.WriteLine(TimeSpan.FromMilliseconds(stopWatch.ElapsedMilliseconds).Seconds);
            System.Console.WriteLine(outputBuffer.ToString());
        }

        public class OutputBuffer
        {
            byte[] _source;
            byte[] _output;
            public Encoding DefaultEncoding = Encoding.UTF8;

            public OutputBuffer(string s, int outputLen)
            {
                _source = Encoding.UTF8.GetBytes(s);
                _output = new byte[outputLen];
            }

            long _srcPtr;
            long _outPtr;
            public void Add(Match match, char[] evalResult)
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
                return DefaultEncoding.GetString(Output);
            }

            public void WrapUp()
            {
                // last match to the end of the file
                Array.Copy(_source, _srcPtr, _output, _outPtr, _source.Length - _srcPtr);
            }

            public byte[] Output => _output;


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

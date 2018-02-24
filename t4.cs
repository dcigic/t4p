using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

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
            var maches = Regex.Matches(template, scopeRegex);

            var q = new Queue<Capture>();

            var lastPtr = 0;

            var firstCapture = template.Substring(0, maches.First().Index);
            q.Enqueue(new Capture(false, firstCapture));

            foreach (Match m in maches)
            {

                q.Enqueue(new Capture(true, m.Value));
                lastPtr = m.Index + m.Value.Length;
                var nextPtr = m.NextMatch()?.Index ?? 0;
                if (nextPtr != 0)
                {
                    var capture = template.Substring(lastPtr, nextPtr - lastPtr);
                    q.Enqueue(new Capture(false, capture));
                }
            }

            var lastCapture = template.Substring(lastPtr, template.Length - lastPtr);
            q.Enqueue(new Capture(false, lastCapture));

            var output = File.CreateText("template.g.cs");
            output.Write(@"namespace exp {");
            output.Write(@"public class Exp {");
            output.Write(@"public static void Main2(string[] args) {");
            while (q.TryDequeue(out Capture c))
            {
                if (c.IsExpression)
                {
                    output.Write(strip(c.Value));
                }
                else
                {
                    output.Write($"System.Console.WriteLine(@\"{c.Value}\");");
                }

            }
            output.Write(@"}");
            output.Write(@"}");
            output.Write(@"}");
            output.Close();


            // Detect the file location for the library that defines the object type
            var systemRefLocation = typeof(object).GetType().Assembly.Location;
            // Create a reference to the library
            var systemReference = MetadataReference.CreateFromFile(systemRefLocation);
            var fileName = "bla.dll";
            var tree = SyntaxFactory.ParseSyntaxTree(File.ReadAllText("template.g.cs"));
            var assemblyPath = Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.Location);
            var Mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var csl = MetadataReference.CreateFromFile(typeof(Console).Assembly.Location);
            List<PortableExecutableReference> refs = new List<PortableExecutableReference>();
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "mscorlib.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Core.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")));
            refs.Add(MetadataReference.CreateFromFile(Assembly.GetEntryAssembly().Location));
            refs.Add(Mscorlib);
            refs.Add(csl);
            var compilation = CSharpCompilation.Create("bla.dll", new[] { tree }, references: refs, options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            compilation.AddReferences(systemReference);
            var emit = compilation.Emit(Path.Combine(Directory.GetCurrentDirectory(), fileName));
            System.Console.WriteLine(emit.Success);
            foreach (var d in emit.Diagnostics)
            {
                System.Console.WriteLine(d.GetMessage());
            }

        }

        struct Capture
        {
            public Capture(bool isExpression, string value)
            {
                IsExpression = isExpression;
                Value = value;
            }
            public bool IsExpression { get; set; }
            public string Value { get; set; }
        }

    }
}

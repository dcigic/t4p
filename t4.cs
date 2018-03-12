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
using System.Runtime.Loader;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis.Text;

namespace c1g1c
{
    class T4
    {
        const string scopeRegex = @"(\$\(.*\))"; // $( exp )
        private string _template;
        private Func<string> _Call;
        static void Main(string[] args)
        {
            args = new string[] { "template" };
            if (!args.Any())
            {
                Console.Error.WriteLine("ERROR: Missing template.");
                return;
            }
            var src = File.ReadAllText(args[0]);
            var consume = new T4(src).Build();

            foreach (var item in Enumerable.Range(0, 100))
            {
                var result = consume();
                File.WriteAllText($"{args[0]}.g", result.ToString());
            }

        }

        public T4(string template)
        {
            _template = template;
        }

        public Func<string> Build()
        { 
            var maches = Regex.Matches(_template, scopeRegex);
            var captures = GetCaptures(_template, maches);
            var method = BuildMethod(captures);
            var tree = SyntaxFactory.ParseSyntaxTree(method);
            var compilation = GetCompilation(tree);
            var assembly = GetAssembly(compilation);
            var callable = GetCallable(assembly);
            return callable;
        }

        private static Func<string> GetCallable(Assembly assembly)
        {
            var method = assembly.GetType("__nsp.__Tpe").GetMethod("__Call");
            var dlg = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), method);
            return dlg;
        }

        private static CSharpCompilation GetCompilation(SyntaxTree tree)
        {
            var refs = new List<PortableExecutableReference>();
            AddReferences(refs);
            return CSharpCompilation.Create(Path.GetRandomFileName(), new[] { tree }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static Assembly GetAssembly(CSharpCompilation compilation)
        {
            using (var stream = new MemoryStream())
            {
                var emit = compilation.Emit(stream);
                System.Console.WriteLine(emit.Success);// replace with NLog
                foreach (var d in emit.Diagnostics)
                {
                    System.Console.WriteLine(d.GetMessage());
                }
                stream.Seek(0, SeekOrigin.Begin);
                var bfr = new byte[stream.Length];
                stream.Read(bfr, 0, bfr.Length);
                return Assembly.Load(bfr);
            }
        }

        private static Queue<Capture> GetCaptures(string template, MatchCollection maches)
        {
            var captures = new Queue<Capture>();

            var lastPtr = 0;
            var firstCapture = template.Substring(0, maches.First().Index);
            captures.Enqueue(new Capture(false, firstCapture));

            foreach (Match m in maches)
            {
                captures.Enqueue(new Capture(true, m.Value));
                lastPtr = m.Index + m.Value.Length;
                var nextPtr = m.NextMatch()?.Index ?? 0;
                if (nextPtr != 0)
                {
                    var capture = template.Substring(lastPtr, nextPtr - lastPtr);
                    captures.Enqueue(new Capture(false, capture));
                }
            }
            var lastCapture = template.Substring(lastPtr, template.Length - lastPtr);
            captures.Enqueue(new Capture(false, lastCapture));

            return captures;
        }

        private static string BuildMethod(Queue<Capture> q)
        {
            Func<string, string> strip = str => str.Remove(str.LastIndexOf(')'), 1).TrimStart('$', '(');

            var output = new StringBuilder();
            output.Append(@"namespace __nsp {");
            output.Append(@"public class __Tpe {");
            output.Append(@"public static string __Call() {");
            output.Append(@"var buffer = new System.Text.StringBuilder(); ");
            while (q.TryDequeue(out Capture c))
            {
                if (c.IsExpression)
                {
                    output.Append(strip(c.Value));
                }
                else
                {
                    output.Append($"buffer.Append(@\"{c.Value}\");");
                }
            }
            output.Append(@" return buffer.ToString();}");
            output.Append(@"}");
            output.Append(@"}");

            return output.ToString();
        }

        private static void AddReferences(List<PortableExecutableReference> refs)
        {
            var assemblyPath = Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.Location);
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "mscorlib.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Core.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")));
            refs.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)); // this will be user injected
            refs.Add(MetadataReference.CreateFromFile(typeof(StringBuilder).Assembly.Location));
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

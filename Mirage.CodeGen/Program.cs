using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mirage.CodeGen;
using Mono.Cecil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace Mirage.Weaver
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Mirage Weaver start");

                if (args.Length == 0)
                {
                    Console.Error.WriteLine("ERROR: Mirage CodeGen cannot be run without any arguments!");
                    Console.WriteLine("Usage: Mirage.CodeGen.exe <path to target DLL> [additional search paths...]");
                    Environment.Exit(1);
                }

                var dllPath = Path.GetFullPath(args[0]);

                // 1. Setup the search directories for the Assembly Resolver
                // This is crucial for resolving Span<T>, int[], etc.
                var searchDirs = new HashSet<string> { Path.GetDirectoryName(dllPath) };
                for (int i = 1; i < args.Length; i++)
                {
                    if (Directory.Exists(args[i]))
                    {
                        searchDirs.Add(Path.GetFullPath(args[i]));
                        Console.WriteLine($"[Resolver] Added search path: {args[i]}");
                    }
                }

                // 2. Create the Resolver
                var resolver = new DefaultAssemblyResolver();
                foreach (var dir in searchDirs)
                {
                    resolver.AddSearchDirectory(dir);
                }

                // 3. Create the CompiledAssembly with the Resolver
                // We pass searchDirs to gather the string[] References the weaver likes to scan
                var compiledAssembly = new CompiledAssembly(dllPath, searchDirs.ToArray(), resolver);

                var weaverLogger = new WeaverLogger(false);
                var weaver = new Weaver(weaverLogger);

                // Weaver.Process will now have access to the Resolver via the compiledAssembly object
                var result = weaver.Process(compiledAssembly);

                Write(result, dllPath, compiledAssembly.PdbPath);

                CheckDiagnostics(weaverLogger);
                Environment.ExitCode = 0;
            }
            catch (Exception e)
            {
                Environment.ExitCode = 1;
                Console.Error.WriteLine(e);
            }
        }

        private static int CheckDiagnostics(WeaverLogger weaverLogger)
        {
            var diagnostics = weaverLogger.GetDiagnostics();
            var exitCode = 0;
            foreach (var message in diagnostics)
            {
                Console.WriteLine($"[{message.DiagnosticType}]: {message.MessageData}");
                if (message.DiagnosticType == DiagnosticType.Error)
                    exitCode = 1;
            }
            return exitCode;
        }

        private static void Write(Result result, string dllPath, string pdbPath)
        {
            if (result.ILPostProcessResult?.InMemoryAssembly == null)
            {
                Console.WriteLine("[Weaver] Warning: No assembly data returned from Weaver. Skipping write.");
                return;
            }

            var inMemory = result.ILPostProcessResult.InMemoryAssembly;

            if (inMemory.PeData != null)
            {
                File.WriteAllBytes(dllPath, inMemory.PeData.ToArray());
                Console.WriteLine($"[Weaver] Success: Wrote woven DLL to {dllPath}");
            }

            if (inMemory.PdbData != null && !string.IsNullOrEmpty(pdbPath))
            {
                File.WriteAllBytes(pdbPath, inMemory.PdbData.ToArray());
                Console.WriteLine($"[Weaver] Wrote woven PDB to {pdbPath}");
            }
        }
    }

    public class CompiledAssembly : ICompiledAssembly
    {
        // Property expected by the Weaver to resolve dependencies
        public IAssemblyResolver Resolver { get; }

        public CompiledAssembly(string dllPath, string[] searchDirs, IAssemblyResolver resolver)
        {
            Name = Path.GetFileName(dllPath);
            PdbPath = Path.ChangeExtension(dllPath, ".pdb");
            Resolver = resolver;

            var peData = File.ReadAllBytes(dllPath);
            byte[] pdbData = File.Exists(PdbPath) ? File.ReadAllBytes(PdbPath) : null;

            InMemoryAssembly = new InMemoryAssembly(peData, pdbData);

            // Gather all DLLs in our search paths to act as References
            References = searchDirs
                .SelectMany(dir => Directory.GetFiles(dir, "*.dll"))
                .ToArray();

            Defines = Array.Empty<string>();
        }

        public InMemoryAssembly InMemoryAssembly { get; }
        public string Name { get; }
        public string PdbPath { get; }
        public string[] References { get; }
        public string[] Defines { get; }
    }
}

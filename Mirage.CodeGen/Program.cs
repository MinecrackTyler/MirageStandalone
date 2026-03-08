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
        private static string _gameDir;

        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (string.IsNullOrEmpty(_gameDir)) return null;

                string assemblyName = new AssemblyName(args.Name).Name;
                string assemblyPath = Path.Combine(_gameDir, assemblyName + ".dll");

                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }

                return null;
            };
        }

        private static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Mirage Weaver start");

                if (args.Length < 2)
                {
                    Console.Error.WriteLine("ERROR: Not enough arguments!");
                    Console.WriteLine("Usage: Mirage.CodeGen.exe <target DLL> <game Managed dir>");
                    Environment.Exit(1);
                }

                var dllPath = Path.GetFullPath(args[0]);
                _gameDir = Path.GetFullPath(args[1]);

                if (!Directory.Exists(_gameDir))
                {
                    Console.Error.WriteLine($"ERROR: Game directory does not exist: {_gameDir}");
                    Environment.Exit(1);
                }

                Console.WriteLine($"[Resolver] Game directory set to: {_gameDir}");

                var searchDirs = new[] { Path.GetDirectoryName(dllPath), _gameDir };
                var resolver = new DefaultAssemblyResolver();
                foreach (var dir in searchDirs)
                {
                    resolver.AddSearchDirectory(dir);
                }

                var compiledAssembly = new CompiledAssembly(dllPath, searchDirs, resolver);
                var weaverLogger = new WeaverLogger(false);
                var weaver = new Weaver(weaverLogger);

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
        public IAssemblyResolver Resolver { get; }

        public CompiledAssembly(string dllPath, string[] searchDirs, IAssemblyResolver resolver)
        {
            Name = Path.GetFileName(dllPath);
            PdbPath = Path.ChangeExtension(dllPath, ".pdb");
            Resolver = resolver;

            var peData = File.ReadAllBytes(dllPath);
            byte[] pdbData = File.Exists(PdbPath) ? File.ReadAllBytes(PdbPath) : null;

            InMemoryAssembly = new InMemoryAssembly(peData, pdbData);

            References = searchDirs
                .Where(Directory.Exists)
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

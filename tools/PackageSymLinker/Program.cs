using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CommandLine;

namespace PackageSymLinker
{
    internal class Options
    {
        [Option('s', "packages-source-dir", HelpText = "The source directory which contains the packages.", Required = true)]
        public string PackagesSourceDir { get; set; }
        
        [Option('t', "package-target-dir", HelpText = "The target directory which has a package manifest.", Required = true)]
        public string PackagesTargetDir { get; set; }

        public string ManifestPath => Path.Combine(PackagesTargetDir, "manifest.json");

        public void CheckAndThrow()
        {
            if (!Directory.Exists(PackagesSourceDir))
            {
                throw new ArgumentException($"Could not find directory at {PackagesSourceDir}");
            }

            if (!Directory.Exists(PackagesTargetDir))
            {
                throw new ArgumentException($"Could not find directory at {PackagesTargetDir}");
            }

            if (!File.Exists(ManifestPath))
            {
                throw new ArgumentException($"Could not find package manifest at {ManifestPath}");
            }
        }
    }
    
    public class Program
    {
        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<Options>(args)
                .MapResult(
                    Run,
                    errors => 1);
        }

        private static int Run(Options options)
        {
            try
            {
                options.CheckAndThrow();

                var dependencies = FindUniqueDependencies(options);

                foreach (var dep in dependencies)
                {
                    var sourceDir = Path.Combine(options.PackagesSourceDir, dep);
                    var targetDir = Path.Combine(options.PackagesTargetDir, dep);

                    MakeSymlink(sourceDir, targetDir);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                Console.Error.WriteLine(e.InnerException);
                return 1;
            }
            
            return 0;
        }

        private static HashSet<string> FindUniqueDependencies(Options options)
        {
            var uniqueDependencies = new HashSet<string>();
            var unvisitedFiles = new Queue<string>();
            var visited = new HashSet<string>();
            
            unvisitedFiles.Enqueue(options.ManifestPath);

            while (unvisitedFiles.Count > 0)
            {
                var manifestFilePath = unvisitedFiles.Dequeue();
                var manifest = PackageModel.From(manifestFilePath);

                visited.Add(manifestFilePath);

                foreach (var dep in manifest.Dependencies)
                {
                    var name = dep.Key;
                    if (!name.StartsWith("io.improbable"))
                    {
                        continue;
                    }
                    
                    // Skip local package references.
                    var source = dep.Value;
                    if (source.StartsWith("file:"))
                    {
                        continue;
                    }

                    var packageDir = Path.Combine(options.PackagesSourceDir, name);

                    if (Directory.Exists(packageDir))
                    {
                        var packagePath = Path.Combine(packageDir, "package.json");
                        if (!visited.Contains(packagePath))
                        {
                            unvisitedFiles.Enqueue(packagePath);
                        }
                        
                        uniqueDependencies.Add(name);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Found dependency {name} in {manifestFilePath}, but could not find a matching package in {options.PackagesSourceDir}");
                    }
                }
            }

            return uniqueDependencies;
        }

        private static void MakeSymlink(string sourcePath, string targetPath)
        {
            ProcessStartInfo startInfo;
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo = new ProcessStartInfo("cmd.exe", $"/c mklink /D {targetPath.Replace("/", "\\")} {sourcePath.Replace("/", "\\")}");
            }
            else
            {
                startInfo = new ProcessStartInfo("ln", $"-s {sourcePath} {targetPath}");
            }

            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;

            using (var proc = Process.Start(startInfo))
            {
                if (proc != null)
                {
                    proc.WaitForExit();

                    if (proc.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"Failed to make a symlink between {sourcePath} and {targetPath}. Output: {proc.StandardError.ReadToEnd()}");
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Packer
{
    public class Program
    {
        private const string ConfigFile = "packer.config.json";

        public static void Main(string[] args)
        {
            var config = ConfigModel.FromFile(Path.Combine(Environment.CurrentDirectory, ConfigFile));

            if (config.GitPackages.Count == 0)
            {
                Console.Error.WriteLine("Packer requires at least one Git package defined. None were found.");
                Environment.Exit(1);
            }

            var tempDir = GetTempDirectory();
            var exitCode = 0;
            
            try
            {
                foreach (var dependency in config.GitPackages)
                {
                    CloneRepo(dependency.CloneUrl, Path.Combine(tempDir, dependency.CloneDir));
                }

                CreatePackage(tempDir, config);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                exitCode = 1;
            }
            finally
            {
                RemoveReadOnlyAttribute(tempDir);
                Directory.Delete(tempDir, true);
                Environment.Exit(exitCode);
            }
        }

        private static string GetTempDirectory()
        {
            var tempDirPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirPath);
            return tempDirPath;
        }

        private static void CreatePackage(string tempDir, ConfigModel config)
        {
            var packageName = Path.Combine(Environment.CurrentDirectory, $"{config.PackageName}-{config.Version}.zip");

            if (File.Exists(packageName))
            {
                File.Delete(packageName);
            }

            using (var archive = ZipFile.Open(packageName, ZipArchiveMode.Create))
            {
                var blackList = config.GitPackages.SelectMany(package =>
                    package.ExcludePaths.Select(subPath => Path.Combine(tempDir, package.CloneDir, subPath))).ToHashSet();

                AddToArchiveRecursive(archive, tempDir, blackList, tempDir.Length + 1);
            }
        }

        private static void AddToArchiveRecursive(ZipArchive archive, string path, HashSet<string> blackList,
            int basePathLength)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                if (blackList.Contains(file))
                {
                    continue;
                }

                // Strip away the base path (temporary directory) length + the leading slash.
                // Replace Windows paths separators with MacOS friendly ones.
                var relativePath = file.Remove(0, basePathLength);
                var fixedPath = relativePath.Replace("\\", "/");
                
                var entry = archive.CreateEntry(fixedPath);
                entry.ExternalAttributes = (int) File.GetAttributes(file);
                using (var stream = entry.Open())
                {
                    stream.Write(File.ReadAllBytes(file));
                }
            }

            foreach (var directory in Directory.GetDirectories(path))
            {
                if (blackList.Contains(directory))
                {
                    continue;
                }

                AddToArchiveRecursive(archive, directory, blackList, basePathLength);
            }
        }

        private static void CloneRepo(string cloneUrl, string repoPath)
        {
            var arguments = string.Join(" ", "clone", cloneUrl, $"\"{repoPath}\"");

            var info = new ProcessStartInfo("git", arguments)
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            using (var process = Process.Start(info))
            {
                if (process == null)
                {
                    throw new Exception("Process failed to start");
                }

                var processOutput = new StringBuilder();

                void OnReceived(object sender, DataReceivedEventArgs args)
                {
                    if (string.IsNullOrEmpty(args.Data))
                    {
                        return;
                    }

                    lock (processOutput)
                    {
                        processOutput.AppendLine(args.Data);
                    }
                }

                process.OutputDataReceived += OnReceived;
                process.ErrorDataReceived += OnReceived;

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Console.Error.WriteLine(processOutput.ToString());
                    throw new Exception($"Process exited with a non-zero error code ({process.ExitCode})");
                }

                Console.WriteLine(processOutput.ToString());
            }
        }


        // Ensure that we can delete the files inside the directory by removing ReadOnly attribute from sub-files.
        private static void RemoveReadOnlyAttribute(string path)
        {
            var subfolders = Directory.GetDirectories(path);

            foreach (var s in subfolders)
            {
                RemoveReadOnlyAttribute(s);
            }

            var files = Directory.GetFiles(path);

            foreach (var f in files)
            {
                var attr = File.GetAttributes(f);

                if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(f, attr ^ FileAttributes.ReadOnly);
                }
            }
        }
    }
}

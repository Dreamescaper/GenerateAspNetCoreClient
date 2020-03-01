using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotNet.Cli.Build
{
    public class Project
    {
        private readonly string _file;
        private readonly string _framework;
        private readonly string _configuration;
        private readonly string _runtime;

        private Project(string file, string framework, string configuration, string runtime)
        {
            _file = file;
            _framework = framework;
            _configuration = configuration;
            _runtime = runtime;

            ProjectName = Path.GetFileName(file);
        }

        public string ProjectName { get; }

        public string AssemblyName { get; set; }
        public string Language { get; set; }
        public string OutputPath { get; set; }
        public string PublishDir { get; set; }
        public string PlatformTarget { get; set; }
        public string ProjectAssetsFile { get; set; }
        public string ProjectDir { get; set; }
        public string RootNamespace { get; set; }
        public string RuntimeFrameworkVersion { get; set; }
        public string TargetFileName { get; set; }
        public string TargetFrameworkMoniker { get; set; }

        public string PublishFilePath => Path.Combine(ProjectDir, PublishDir, TargetFileName);
        public string OutputFilePath => Path.Combine(ProjectDir, OutputPath, TargetFileName);

        public static Project FromPath(
            string path,
            string buildExtensionsDir = null,
            string framework = null,
            string configuration = null,
            string runtime = null)
        {
            var file = GetProjectFilePath(path);

            if (buildExtensionsDir == null)
            {
                buildExtensionsDir = Path.Combine(Path.GetDirectoryName(file), "obj");
            }

            Directory.CreateDirectory(buildExtensionsDir);

            var dotnetCliTargetsPath = Path.Combine(
                buildExtensionsDir,
                Path.GetFileName(file) + ".DotNetCliBuild.targets");
            using (var input = typeof(Project).Assembly.GetManifestResourceStream(
                "DotNet.Cli.Build.Resources.DotNetCliBuild.targets"))

            using (var output = File.OpenWrite(dotnetCliTargetsPath))
            {
                input.CopyTo(output);
            }

            IDictionary<string, string> metadata;
            var metadataFile = Path.GetTempFileName();
            try
            {
                var propertyArg = "/property:DotNetCliBuildMetadataFile=" + metadataFile;
                if (framework != null)
                {
                    propertyArg += ";TargetFramework=" + framework;
                }

                if (configuration != null)
                {
                    propertyArg += ";Configuration=" + configuration;
                }

                if (runtime != null)
                {
                    propertyArg += ";RuntimeIdentifier=" + runtime;
                }

                var args = new List<string>
                {
                    "msbuild",
                    "/target:__GetProjectMetadata",
                    propertyArg,
                    "/verbosity:quiet",
                    "/nologo"
                };

                if (file != null)
                {
                    args.Add(file);
                }

                var exitCode = Exe.Run("dotnet", args);
                if (exitCode != 0)
                {
                    throw new Exception("Unable to retrieve project metadata.");
                }

                metadata = File.ReadLines(metadataFile)
                    .Select(l => l.Split(new[] { ':' }, 2))
                    .ToDictionary(s => s[0], s => s[1].TrimStart());
            }
            finally
            {
                File.Delete(metadataFile);
            }

            var platformTarget = metadata["PlatformTarget"];
            if (platformTarget.Length == 0)
            {
                platformTarget = metadata["Platform"];
            }

            return new Project(file, framework, configuration, runtime)
            {
                AssemblyName = metadata["AssemblyName"],
                Language = metadata["Language"],
                OutputPath = metadata["OutputPath"],
                PublishDir = metadata["PublishDir"],
                PlatformTarget = platformTarget,
                ProjectAssetsFile = metadata["ProjectAssetsFile"],
                ProjectDir = metadata["ProjectDir"],
                RootNamespace = metadata["RootNamespace"],
                RuntimeFrameworkVersion = metadata["RuntimeFrameworkVersion"],
                TargetFileName = metadata["TargetFileName"],
                TargetFrameworkMoniker = metadata["TargetFrameworkMoniker"]
            };
        }

        public void Build()
        {
            var args = new List<string> { "build" };

            if (_file != null)
            {
                args.Add(_file);
            }

            if (_framework != null)
            {
                args.Add("--framework");
                args.Add(_framework);
            }

            if (_configuration != null)
            {
                args.Add("--configuration");
                args.Add(_configuration);
            }

            if (_runtime != null)
            {
                args.Add("--runtime");
                args.Add(_runtime);
            }

            args.Add("/verbosity:quiet");
            args.Add("/nologo");

            var exitCode = Exe.Run("dotnet", args, interceptOutput: true);
            if (exitCode != 0)
            {
                throw new Exception("BuildFailed");
            }
        }

        public void Publish()
        {
            var args = new List<string> { "publish" };

            if (_file != null)
            {
                args.Add(_file);
            }

            if (_framework != null)
            {
                args.Add("--framework");
                args.Add(_framework);
            }

            if (_configuration != null)
            {
                args.Add("--configuration");
                args.Add(_configuration);
            }

            if (_runtime != null)
            {
                args.Add("--runtime");
                args.Add(_runtime);
            }

            args.Add("/verbosity:quiet");
            args.Add("/nologo");

            var exitCode = Exe.Run("dotnet", args, interceptOutput: true);
            if (exitCode != 0)
            {
                throw new Exception("PublishFailed");
            }
        }

        private static string GetProjectFilePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            if (File.Exists(path))
            {
                // If path is file - return as is
                return path;
            }
            else if (Directory.Exists(path))
            {
                // If path is directory - try finding csproj file
                var csprojFiles = Directory.GetFiles(path, "*.csproj");

                if (csprojFiles.Length == 0)
                    throw new ArgumentException("No project files found");

                if (csprojFiles.Length > 1)
                    throw new ArgumentException("Multiple project files found");

                return csprojFiles[0];
            }
            else
            {
                throw new ArgumentException("Specified path does not exist");
            }
        }
    }
}

using System;
using System.IO;
using System.Text.RegularExpressions;
using DotNet.Cli.Build;
using GenerateAspNetCoreClient.Options;
using NUnit.Framework;

namespace GenerateAspNetCoreClient.Tests
{
    [NonParallelizable]
    public class ClientGenerationTests
    {
        private static readonly string _snapshotsPath = Path.Combine("..", "..", "..", "__snapshots__", "{0}", "{1}");

        private static readonly string _inputPath = Path.Combine("..", "..", "..", "..", "{0}", "{0}.{1}.csproj");
        private static readonly string _outPath = Path.Combine("..", "..", "..", "..", "OutputTest", "Client");
        private static readonly string _outProjectPath = Path.Combine("..", "..", "..", "..", "OutputTest", "OutputProject.csproj");

        private static readonly bool _regenerateSnapshots = false;

        [TearDown]
        public void CleanOutput()
        {
            if (Directory.Exists(_outPath))
            {
                Directory.Delete(_outPath, true);
            }
        }

        [TestCase("TestWebApi.Controllers", "net8.0")]
        [TestCase("TestWebApi.Controllers", "net9.0")]
        [TestCase("TestWebApi.Versioning", "net8.0")]
        [TestCase("TestWebApi.Versioning", "net9.0")]
        [TestCase("TestWebApi.MinimalApi", "net8.0")]
        [TestCase("TestWebApi.MinimalApi", "net9.0")]
        public void GenerationTest(string testProjectName, string framework)
        {
            var path = string.Format(_inputPath, testProjectName, framework);
            var options = new GenerateClientOptions
            {
                InputPath = path,
                OutPath = _outPath,
                Namespace = "Test.Name.Space",
                BuildExtensionsDir = Path.Combine(Path.GetDirectoryName(path), "obj", framework)
            };

            Program.CreateClient(options);

            Assert.That(() => Project.FromPath(_outProjectPath).Build(), Throws.Nothing);
            AssertSnapshotMatch(testProjectName, framework);
        }

        [TestCase("net8.0")]
        [TestCase("net9.0")]
        public void GenerationTest_UseApiResponses(string framework)
        {
            var path = string.Format(_inputPath, "TestWebApi.Controllers.UseApiResponses", framework);
            var options = new GenerateClientOptions
            {
                InputPath = path,
                UseApiResponses = true,
                OutPath = _outPath,
                Namespace = "Test.Name.Space",
                BuildExtensionsDir = Path.Combine(Path.GetDirectoryName(path), "obj", framework)
            };

            Program.CreateClient(options);

            Assert.That(() => Project.FromPath(_outProjectPath).Build(), Throws.Nothing);
            AssertSnapshotMatch("TestWebApi.Controllers.UseApiResponses", framework);
        }

        private static void RegenerateSnapshots(string testProjectName, string framework)
        {
            var snapshotsPath = string.Format(_snapshotsPath, testProjectName, framework);

            if (Directory.Exists(snapshotsPath))
            {
                Directory.Delete(snapshotsPath, recursive: true);
            }

            Directory.CreateDirectory(snapshotsPath);

            var generatedFiles = Directory.EnumerateFiles(_outPath, "*", new EnumerationOptions { RecurseSubdirectories = true });

            foreach (var generatedFile in generatedFiles)
            {
                var relativePath = Path.GetRelativePath(_outPath, generatedFile);
                File.Move(generatedFile, Path.Combine(snapshotsPath, relativePath + ".snap"));
            }

            throw new Exception("Don't forget to disable snapshot regeneration.");
        }

        private static void AssertSnapshotMatch(string testProjectName, string framework)
        {
            if (_regenerateSnapshots)
            {
                RegenerateSnapshots(testProjectName, framework);
            }

            var snapshotsPath = string.Format(_snapshotsPath, testProjectName, framework);
            var generatedFiles = Directory.EnumerateFiles(_outPath, "*", new EnumerationOptions { RecurseSubdirectories = true });

            foreach (var generatedFile in generatedFiles)
            {
                var relativePath = Path.GetRelativePath(_outPath, generatedFile);
                var snapshotPath = Path.Combine(snapshotsPath, relativePath + ".snap");

                Assert.That(snapshotPath, Does.Exist, $"Unexpected file generated ({relativePath})");

                var actualContent = File.ReadAllText(generatedFile);
                var expectedContent = File.ReadAllText(snapshotPath);

                Assert.That(actualContent, Is.EqualTo(expectedContent).Using<string>(IgnoreLineEndings),
                    $"File mismatch (${relativePath})");
            }

            static bool IgnoreLineEndings(string s1, string s2)
            {
                s1 = Regex.Replace(s1, "(\r|\n|\r\n)", "\r");
                s2 = Regex.Replace(s2, "(\r|\n|\r\n)", "\r");
                return s1 == s2;
            }
        }
    }
}

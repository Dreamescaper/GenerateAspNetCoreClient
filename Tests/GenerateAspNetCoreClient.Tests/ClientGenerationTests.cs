using System.IO;
using System.Text.RegularExpressions;
using DotNet.Cli.Build;
using GenerateAspNetCoreClient.Options;
using NUnit.Framework;

#pragma warning disable IDE0051 // Remove unused private members

namespace GenerateAspNetCoreClient.Tests
{
    public class ClientGenerationTests
    {
        private static readonly string _snapshotsPath = Path.Combine("..", "..", "..", "__snapshots__");

        private static readonly string _inputPath = Path.Combine("..", "..", "..", "..", "ApiExplorerTest", "TestWebApi.csproj");
        private static readonly string _outPath = Path.Combine("..", "..", "..", "..", "OutputTest", "Client");
        private static readonly string _outProjectPath = Path.Combine("..", "..", "..", "..", "OutputTest", "OutputProject.csproj");

        [TearDown]
        public void CleanOutput()
        {
            Directory.Delete(_outPath, true);
        }

        [Test]
        public void GenerationTest()
        {
            var options = new GenerateClientOptions
            {
                InputPath = _inputPath,
                OutPath = _outPath,
                Namespace = "Test.Name.Space",
            };

            GenerateAspNetCoreClient.Program.CreateClient(options);

            Assert.That(() => Project.FromPath(_outProjectPath).Build(), Throws.Nothing);
            AssertSnapshotMatch("netcoreapp3.1");
        }

        private void RegenerateSnapshots(string name)
        {
            var setPath = Path.Combine(_snapshotsPath, name);

            if (Directory.Exists(setPath))
            {
                Directory.Delete(setPath);
            }

            Directory.CreateDirectory(setPath);

            var generatedFiles = Directory.EnumerateFiles(_outPath, "*", new EnumerationOptions { RecurseSubdirectories = true });

            foreach (var generatedFile in generatedFiles)
            {
                var relativePath = Path.GetRelativePath(_outPath, generatedFile);
                File.Move(generatedFile, Path.Combine(setPath, relativePath + ".snap"));
            }
        }

        private void AssertSnapshotMatch(string name)
        {
            var setPath = Path.Combine(_snapshotsPath, name);

            var generatedFiles = Directory.EnumerateFiles(_outPath, "*", new EnumerationOptions { RecurseSubdirectories = true });

            foreach (var generatedFile in generatedFiles)
            {
                var relativePath = Path.GetRelativePath(_outPath, generatedFile);
                var snapshotPath = Path.Combine(setPath, relativePath + ".snap");

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

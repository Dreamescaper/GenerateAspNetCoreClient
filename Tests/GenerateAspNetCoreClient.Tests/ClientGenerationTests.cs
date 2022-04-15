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
        private static readonly string _snapshotsPath = Path.Combine("..", "..", "..", "__snapshots__", "{0}");

        private static readonly string _inputPath = Path.Combine("..", "..", "..", "..", "{0}", "{0}.csproj");
        private static readonly string _outPath = Path.Combine("..", "..", "..", "..", "OutputTest", "Client");
        private static readonly string _outProjectPath = Path.Combine("..", "..", "..", "..", "OutputTest", "OutputProject.csproj");

        [TearDown]
        public void CleanOutput()
        {
            Directory.Delete(_outPath, true);
        }

        [TestCase("TestWebApi.Controllers")]
        [TestCase("TestWebApi.Versioning")]
        public void GenerationTest(string testProjectName)
        {
            var options = new GenerateClientOptions
            {
                InputPath = string.Format(_inputPath, testProjectName),
                OutPath = _outPath,
                Namespace = "Test.Name.Space",
            };

            Program.CreateClient(options);

            Assert.That(() => Project.FromPath(_outProjectPath).Build(), Throws.Nothing);

            // Uncomment when needed to regenerate snapshots.
            RegenerateSnapshots(testProjectName);
            AssertSnapshotMatch(testProjectName);
        }

        private static void RegenerateSnapshots(string testProjectName)
        {
            var snapshotsPath = string.Format(_snapshotsPath, testProjectName);

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
        }

        private static void AssertSnapshotMatch(string testProjectName)
        {
            var snapshotsPath = string.Format(_snapshotsPath, testProjectName);
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

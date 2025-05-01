using System.IO;
using System.Threading.Tasks;
using DotNet.Cli.Build;
using GenerateAspNetCoreClient.Options;
using NUnit.Framework;
using VerifyNUnit;

namespace GenerateAspNetCoreClient.Tests
{
    [NonParallelizable]
    public class ClientGenerationTests
    {
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
        [TestCase("TestWebApi.MinimalApi")]
        public async Task GenerationTest(string testProjectName)
        {
            var options = new GenerateClientOptions
            {
                InputPath = string.Format(_inputPath, testProjectName),
                OutPath = _outPath,
                Namespace = "Test.Name.Space",
            };
        
            Program.CreateClient(options);
        
            Assert.That(() => Project.FromPath(_outProjectPath).Build(), Throws.Nothing);
            await Verifier.VerifyDirectory(_outPath);
        }

        [Test]
        public async Task GenerationTest_UseApiResponses()
        {
            var options = new GenerateClientOptions
            {
                InputPath = string.Format(_inputPath, "TestWebApi.Controllers.UseApiResponses"),
                UseApiResponses = true,
                OutPath = _outPath,
                Namespace = "Test.Name.Space",
            };

            Program.CreateClient(options);

            Assert.That(() => Project.FromPath(_outProjectPath).Build(), Throws.Nothing);
            await Verifier.VerifyDirectory(_outPath);
        }
        
        [Test]
        public async Task GenerationTest_UseApiResponses_UseCancellationTokens()
        {
            var options = new GenerateClientOptions
            {
                InputPath = string.Format(_inputPath,"TestWebApi.MinimalApi"),
                UseApiResponses = true,
                AddCancellationTokenParameters = true,
                OutPath = _outPath,
                Namespace = "Test.Name.Space",
            };
        
            Program.CreateClient(options);
        
            Assert.That(() => Project.FromPath(_outProjectPath).Build(), Throws.Nothing);
            await Verifier.VerifyDirectory(_outPath);
        }
    }
}

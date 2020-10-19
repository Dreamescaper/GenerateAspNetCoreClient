using System.Linq;
using GenerateAspNetCoreClient.Command;
using GenerateAspNetCoreClient.Options;
using NUnit.Framework;

namespace GenerateAspNetCoreClient.Tests
{
    public class EndpointFilteringTests
    {
        [Test]
        public void ExcludeSpecifiedTypes()
        {
            // Arrange
            var options = new GenerateClientOptions { ExcludeTypes = "FilterName" };

            var apiDescriptions = new[]
                {
                    typeof(FilterNameType),
                    typeof(OtherNameType),
                    typeof(FilterNameNamespace.SomeType1),
                    typeof(OtherNameNamespace.SomeType2)
                }
                .Select(type => ApiDescriptionTestData.CreateApiDescription(controllerType: type))
                .ToArray();

            var apiExplorer = ApiDescriptionTestData.CreateApiExplorer(apiDescriptions);

            // Act
            var clients = new ClientModelBuilder(apiExplorer, options, new string[] { }).GetClientCollection();

            // Assert
            Assert.That(clients.Select(c => c.Name), Is.EquivalentTo(new[] { "IOtherNameTypeApi", "ISomeType2Api" }));
        }

        [Test]
        public void IncludeSpecifiedTypes()
        {
            // Arrange
            var options = new GenerateClientOptions { IncludeTypes = "FilterName" };

            var apiDescriptions = new[]
                {
                    typeof(FilterNameType),
                    typeof(OtherNameType),
                    typeof(FilterNameNamespace.SomeType1),
                    typeof(OtherNameNamespace.SomeType2)
                }
                .Select(type => ApiDescriptionTestData.CreateApiDescription(controllerType: type))
                .ToArray();

            var apiExplorer = ApiDescriptionTestData.CreateApiExplorer(apiDescriptions);

            // Act
            var clients = new ClientModelBuilder(apiExplorer, options, new string[] { }).GetClientCollection();

            // Assert
            Assert.That(clients.Select(c => c.Name), Is.EquivalentTo(new[] { "IFilterNameTypeApi", "ISomeType1Api" }));
        }

        [Test]
        public void ExcludeSpecifiedPaths()
        {
            // Arrange
            var options = new GenerateClientOptions { ExcludePaths = "filter-path" };

            var apiDescriptions = new[]
                {
                    "filter-path/items",
                    "filter-path-items",
                    "other-path",
                    "other-path-items",
                }
                .Select(path => ApiDescriptionTestData.CreateApiDescription(path: path))
                .ToArray();

            var apiExplorer = ApiDescriptionTestData.CreateApiExplorer(apiDescriptions);

            // Act
            var clients = new ClientModelBuilder(apiExplorer, options, new string[] { }).GetClientCollection();

            // Assert
            Assert.That(clients.SelectMany(c => c.EndpointMethods).Select(e => e.Path),
                Is.EquivalentTo(new[] { "other-path", "other-path-items" }));
        }

        [Test]
        public void IncludeSpecifiedPaths()
        {
            // Arrange
            var options = new GenerateClientOptions { IncludePaths = "filter-path" };

            var apiDescriptions = new[]
                {
                    "filter-path/items",
                    "filter-path-items",
                    "other-path",
                    "other-path-items",
                }
                .Select(path => ApiDescriptionTestData.CreateApiDescription(path: path))
                .ToArray();

            var apiExplorer = ApiDescriptionTestData.CreateApiExplorer(apiDescriptions);

            // Act
            var clients = new ClientModelBuilder(apiExplorer, options, new string[] { }).GetClientCollection();

            // Assert
            Assert.That(clients.SelectMany(c => c.EndpointMethods).Select(e => e.Path),
                Is.EquivalentTo(new[] { "filter-path/items", "filter-path-items", }));
        }
    }

    #region Test Classes

    internal class FilterNameType { }
    internal class OtherNameType { }

    namespace FilterNameNamespace
    {
        internal class SomeType1 { }
    }

    namespace OtherNameNamespace
    {
        internal class SomeType2 { }
    }

    #endregion
}

using System;
using System.Linq;
using System.Threading;
using GenerateAspNetCoreClient.Command;
using GenerateAspNetCoreClient.Command.Model;
using GenerateAspNetCoreClient.Options;
using NUnit.Framework;

namespace GenerateAspNetCoreClient.Tests
{
    public class ClientModelBuilderTests
    {
        [Test]
        public void AddCancellationTokenParameterIfRequested()
        {
            // Arrange
            var options = new GenerateClientOptions { AddCancellationTokenParameters = true };
            var existingParameter = ApiDescriptionTestData.CreateParameter();
            var apiExplorer = ApiDescriptionTestData.CreateApiExplorer(apiParameters: new[] { existingParameter });
            var builder = new ClientModelBuilder(apiExplorer, options, Array.Empty<string>());

            // Act
            var client = builder.GetClientCollection().Clients[0];
            var parameters = client.EndpointMethods[0].Parameters;

            // Assert
            Assert.That(client.ImportedNamespaces, Does.Contain("System.Threading"));

            Assert.That(parameters.Count, Is.EqualTo(2), "(existing and cancellationToken)");

            var cancellationTokenParameter = parameters.Last();

            Assert.That(cancellationTokenParameter.Type, Is.EqualTo(typeof(CancellationToken)));
            Assert.That(cancellationTokenParameter.Name, Is.EqualTo("cancellationToken"));
            Assert.That(cancellationTokenParameter.DefaultValueLiteral, Is.EqualTo("default"));
            Assert.That(cancellationTokenParameter.Source, Is.EqualTo(ParameterSource.Other));
        }
    }
}

using Microsoft.Extensions.Configuration;
using ModelBuilder;
using ModelBuilder.TypeCreators;
using ModelBuilder.ValueGenerators;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
namespace EarlyXrm.QueryExpressionExtensions.IntegrationTests
{
    public abstract class IntegrationTestsBase
    {
        protected IBuildConfiguration Builder;

        protected IBuildConfiguration NumericBuilder;

        protected string connectionString;

        public IntegrationTestsBase()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddUserSecrets<ReportsIntegrationTests>()
                .Build();

            connectionString = config.GetConnectionString("Crm");

            NumericBuilder = Model.UsingDefaultConfiguration()
                .UpdateValueGenerator<NumericValueGenerator>(x => x.AllowNegative = false);

            Builder = Model
                .UsingDefaultConfiguration()
                .UpdateTypeCreator<EnumerableTypeCreator>(x => { x.MinCount = 1; x.MaxCount = 5; })
                .UsingModule<DynamicsModule>();
        }
    }
}
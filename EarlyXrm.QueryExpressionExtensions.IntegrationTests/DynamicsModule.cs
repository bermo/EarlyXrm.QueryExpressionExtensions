using Microsoft.Xrm.Sdk;
using ModelBuilder;
using System.Linq;

namespace EarlyXrm.QueryExpressionExtensions.IntegrationTests
{
    public class DynamicsModule : IConfigurationModule
    {
        public void Configure(IBuildConfiguration configuration)
        {
            configuration
                .AddIgnoreRule(x => x.PropertyType.IsClass && x.PropertyType != typeof(string) || (x.PropertyType?.GetGenericArguments()?.FirstOrDefault()?.IsClass ?? false))
                .AddIgnoreRule(x => x.PropertyType == typeof(EntityReference))
                .AddIgnoreRule(x => x.Name == nameof(Entity.EntityState))
                .AddIgnoreRule(x => x.Name == nameof(Entity.KeyAttributes))
                .AddIgnoreRule(x => x.Name == nameof(Entity.LazyFileAttributeKey))
                .AddIgnoreRule(x => x.Name == nameof(Entity.LazyFileAttributeValue))
                .AddIgnoreRule(x => x.Name == nameof(Entity.LazyFileSizeAttributeKey))
                .AddIgnoreRule(x => x.Name == nameof(Entity.LazyFileSizeAttributeValue))
                .AddIgnoreRule(x => x.Name == nameof(Entity.RowVersion))
                .AddIgnoreRule(x => x.Name == nameof(Entity.ExtensionData))
                .AddIgnoreRule(x => x.Name == nameof(Entity.Attributes))
                .AddIgnoreRule(x => x.Name == nameof(Entity.LazyFileSizeAttributeValue))
                .AddIgnoreRule<Entity>(x => x.LogicalName)
                .AddIgnoreRule(x => x.Name == "CreatedOn")
                .AddIgnoreRule(x => x.Name == "ModifiedOn")
                ;
        }
    }
}
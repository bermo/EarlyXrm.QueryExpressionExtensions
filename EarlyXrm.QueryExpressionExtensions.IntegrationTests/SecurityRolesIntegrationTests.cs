using EarlyBoundTypes;
using FluentAssertions;
using Microsoft.PowerPlatform.Dataverse.Client;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.IntegrationTests
{
    public class SecurityRolesIntegrationTests : IntegrationTestsBase
    {
        [Fact]
        public void CheckThatAllRolesHavePrivileges()
        {
            using var ctx = new ServiceClient(connectionString);

            var securityRoles = new QueryExpression<SecurityRole>
            {
                LinkEntities = {
                    new LinkEntity<SecurityRole>(x => x.Privileges)
                }
            }.RetrieveMultiple(ctx).Entities;

            securityRoles.Should().NotBeEmpty();
            foreach (var role in securityRoles)
                role.Privileges.Should().NotBeEmpty();
        }
    }
}
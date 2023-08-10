using FluentAssertions.Equivalency;

namespace EarlyXrm.QueryExpressionExtensions.IntegrationTests
{
    public partial class ManyToManyQueryIntegrationTests
    {
        class IgnoreNullMembersInExpectation : IEquivalencyStep
        {
            public EquivalencyResult Handle(Comparands comparands, IEquivalencyValidationContext context, IEquivalencyValidator nestedValidator) => EquivalencyResult.ContinueWithNext;
        }
    }
}
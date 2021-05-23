using EarlyBoundTypes;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Tooling.Connector;
using ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.IntegrationTests
{
    public partial class ManyToManyQueryIntegrationTests : IntegrationTestsBase
    {
        [Fact]
        public void AddAndGetKnowledgeArticles()
        {
            using var ctx = new CrmServiceClient(connectionString);

            var categories = new QueryExpression<KnowledgeArticle>().RetrieveMultiple(ctx).Entities;
            var languages = new QueryExpression<Language>().RetrieveMultiple(ctx).Entities.Where(x => x.Language2 == "English").OrderBy(x => x.Name);

            var knowledgeArticle = Builder.Create<KnowledgeArticle>()
                .Set(x => x.Language = languages.FirstOrDefault(x => x.LocaleID == 1033).ToEntityReference())
                .Set(x => x.Status = KnowledgeArticle.Enums.Status.Draft)
                .Set(x => x.StatusReason = KnowledgeArticle.Enums.StatusReason.Draft)
                .Set(x => x.IsLatestVersion = true)
                .Set(x => x.PrimaryArticle = null)
                .Set(x => x.ReadyForReview = true)
                .Set(x => x.Categories = Builder.Create<IEnumerable<Category>>());

            try
            {
                var id = ctx.Create(knowledgeArticle);

                var result = new QueryExpression<KnowledgeArticle>
                {
                    Conditions = { new ConditionExpression<KnowledgeArticle>(x => x.Id, id) },
                    LinkEntities = { new LinkEntity<KnowledgeArticle, Category>(x => x.Categories) }
                }.RetrieveMultiple(ctx).Entities.First();

                result.Should().BeEquivalentTo(knowledgeArticle, x => x
                    .Using<DateTime>(y => y.Subject.Should().BeCloseTo(y.Expectation, 1000)).WhenTypeIs<DateTime>()
                    .Excluding(y => y.SelectedMemberInfo.MemberType == typeof(EntityReference))
                    .Excluding(y => y.SelectedMemberInfo.DeclaringType == typeof(Entity))
                    .Using(new IgnoreNullMembersInExpectation())
                    .Excluding(x => x.IsLatestVersion)
                    .Excluding(x => x.PrimaryArticle)
                    .Excluding(y => y.Categories));

                result.Categories.Should().BeEquivalentTo(knowledgeArticle.Categories, x => x
                    .Excluding(y => y.SelectedMemberInfo.MemberType == typeof(EntityReference))
                    .Excluding(y => y.SelectedMemberInfo.DeclaringType == typeof(Entity))
                    .Using(new IgnoreNullMembersInExpectation()));
            }
            finally
            {
                ctx.Execute(new DisassociateRequest
                {
                    Target = knowledgeArticle.ToEntityReference(),
                    RelatedEntities = new EntityReferenceCollection(knowledgeArticle.Categories.Select(x => x.ToEntityReference()).ToList()),
                    Relationship = new Relationship(KnowledgeArticle.Relationships.Categories)
                });

                foreach (var cat in knowledgeArticle.Categories)
                {
                    ctx.Delete(cat.LogicalName, cat.Id);
                }

                ctx.Delete(knowledgeArticle.LogicalName, knowledgeArticle.Id);
            }
        }
    }
}
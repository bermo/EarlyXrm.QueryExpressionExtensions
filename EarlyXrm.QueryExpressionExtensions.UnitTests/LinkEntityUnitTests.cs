using EarlyXrm.QueryExpressionExtensions.UnitTests.Model;
using Microsoft.Xrm.Sdk.Query;
using System;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests
{
    public class LinkEntityUnitTests
    {
        [Fact]
        public void LinkEntityOneToMany_PopulatedAsExpected()
        {
            var le = new LinkEntity<Test>(x => x.TestChilds);

            var result = (LinkEntity)le;

            Assert.Equal("ee_testid", result.LinkFromAttributeName);
            Assert.Equal("ee_test", result.LinkFromEntityName);
            Assert.Equal("ee_testid", result.LinkToAttributeName);
            Assert.Equal("ee_testchild", result.LinkToEntityName);
        }

        [Fact]
        public void LinkEntityManyToOne_PopulatedAsExpected()
        {
            var le = new LinkEntity<TestChild>(x => x.Test);

            var result = (LinkEntity)le;

            Assert.Equal("ee_testid", result.LinkFromAttributeName);
            Assert.Equal("ee_testchild", result.LinkFromEntityName);
            Assert.Equal("ee_testid", result.LinkToAttributeName);
            Assert.Equal("ee_test", result.LinkToEntityName);
        }

        [Fact]
        public void LinkEntityManyToMany_PopulatedAsExpected()
        {
            var le = new LinkEntity<Test>(x => x.TestManys);

            var result = (LinkEntity)le;

            Assert.Equal("ee_testid", result.LinkFromAttributeName);
            Assert.Equal("ee_test", result.LinkFromEntityName);
            Assert.Equal("ee_testid", result.LinkToAttributeName);
            Assert.Equal("ee_test_testmanys", result.LinkToEntityName);
            var subActual = result.LinkEntities[0];
            Assert.Equal("ee_testmanyid", subActual.LinkFromAttributeName);
            Assert.Equal("ee_test_testmanys", subActual.LinkFromEntityName);
            Assert.Equal("ee_testmanyid", subActual.LinkToAttributeName);
            Assert.Equal("ee_testmany", subActual.LinkToEntityName);
        }

        [Fact]
        public void LinkEntityFilterCriteria_PopulatedAsExpected()
        {
            var id = Guid.NewGuid();
            var le = new LinkEntity<Test, TestChild>(x => x.TestChilds)
            {
                LinkConditions =
                {
                    new ConditionExpression<TestChild>(x => x.Id, id)
                }
            };

            var result = (LinkEntity)le;

            var criteria = Assert.Single(result.LinkCriteria.Conditions);
            Assert.Equal("ee_testchildid", criteria.AttributeName);
        }

        [Fact]
        public void LinkEntityWithSubLinkEntity_PopulatedAsExpected()
        {
            var id = Guid.NewGuid();
            var linkEntity = new LinkEntity<TestChild, Test>(x => x.Test)
            {
                LinkEntities =
                {
                    new LinkEntity<Test, Test>(x => x.ParentTestTests)
                    {
                        LinkConditions =
                        {
                            new ConditionExpression<Test>(x => x.Id, id)
                        }
                    }
                }
            };

            var result = (LinkEntity)linkEntity;

            var subLinkEntity = Assert.Single(result.LinkEntities);
            Assert.Equal("ee_testid", subLinkEntity.LinkFromAttributeName);
            Assert.Equal("ee_test", subLinkEntity.LinkFromEntityName);
            Assert.Equal("ee_parenttestid", subLinkEntity.LinkToAttributeName);
            Assert.Equal("ee_test", subLinkEntity.LinkToEntityName);
            var subLinkEntityCondition = Assert.Single(subLinkEntity.LinkCriteria.Conditions);
            Assert.Equal("ee_testid", subLinkEntityCondition.AttributeName);
            Assert.Equal(new object[] { id }, subLinkEntityCondition.Values);
        }

        [Fact]
        public void LinkEntityWithSubSubLinkEntity_PopulatedAsExpected()
        {
            var linkEntity = new LinkEntity<TestChild, Test>(x => x.Test)
            {
                LinkEntities =
                {
                    new LinkEntity<Test, Test>(x => x.ParentTestTests)
                    {
                        EntityAlias = "ParentTestTests",
                        LinkEntities =
                        {
                            new LinkEntity<Test, TestChild>(x => x.TestChilds)
                            {
                                EntityAlias = "ParentTestTests.TestChilds",
                                Columns = new ColumnSet<TestChild>(x => x.Test),
                                LinkConditions =
                                {
                                    new ConditionExpression<TestChild>(x => x.TestId, ConditionOperator.NotNull)
                                },
                                Orders =
                                {
                                    new OrderExpression<TestChild>(x => x.TestId, OrderType.Descending)
                                }
                            }
                        }
                    }
                }
            };

            var result = (LinkEntity)linkEntity;

            var subSubLinkEntity = Assert.Single(Assert.Single(result.LinkEntities).LinkEntities);
            Assert.Equal("ee_testid", subSubLinkEntity.LinkFromAttributeName);
            Assert.Equal("ee_test", subSubLinkEntity.LinkFromEntityName);
            Assert.Equal("ee_testid", subSubLinkEntity.LinkToAttributeName);
            Assert.Equal("ee_testchild", subSubLinkEntity.LinkToEntityName);
            Assert.Equal("ParentTestTests.TestChilds", subSubLinkEntity.EntityAlias);
            var subSubLinkEntityCondition = Assert.Single(subSubLinkEntity.LinkCriteria.Conditions);
            Assert.Equal("ee_testid", subSubLinkEntityCondition.AttributeName);
            Assert.Equal(ConditionOperator.NotNull, subSubLinkEntityCondition.Operator);
            Assert.Equal(new object[] { }, subSubLinkEntityCondition.Values);
        }
    }
}
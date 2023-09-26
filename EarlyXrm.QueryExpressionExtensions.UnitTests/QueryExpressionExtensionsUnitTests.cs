using EarlyXrm.QueryExpressionExtensions.UnitTests.Model;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests
{
    public class QueryExpressionExtensionsUnitTests
    {
        private IOrganizationService service;

        public QueryExpressionExtensionsUnitTests()
        {
            service = Substitute.For<IOrganizationService>();
        }

        [Fact]
        public void RetrieveMultipleGeneric()
        {
            var qe = new QueryExpression<Test>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection<Test> { new Test() });

            var result  = service.RetrieveMultiple<Test>(qe);

            Assert.Single(result.Entities);
        }

        [Fact]
        public void RetrieveSingleGeneric()
        {
            var id = Guid.NewGuid();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection<Test> {
                new Test { Id = id, DayOfWeek = DayOfWeek.Sunday },
                new Test { Id = Guid.NewGuid() }
            });

            var result = service.Retrieve(id, new ColumnSet<Test>(x => x.Id, x => x.DayOfWeek!));

            Assert.Equal(new[] { "ee_testid", "ee_dayofweek" }, result?.Attributes.Select(x => x.Key));
        }

        [Fact]
        public void RetrieveMultipleQueryExtension()
        {
            var qe = new QueryExpression<Test>();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection<Test> { new Test() });

            var result = qe.RetrieveMultiple(service);

            Assert.Single(result.Entities);
        }

        [Fact]
        public void RetrieveSingleQueryExtension()
        {
            var id = Guid.NewGuid();
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(new EntityCollection<Test> {
                new Test { Id = id, DayOfWeek = DayOfWeek.Sunday },
                new Test { Id = Guid.NewGuid() }
            });

            var result = new QueryExpression<Test>().Retrieve(service, id);

            Assert.Equal(id, result?.Id);
            Assert.Equal(DayOfWeek.Sunday, result?.DayOfWeek);
        }

        [Fact]
        public void RetrieveMultipleGenericViaOrgService()
        {
            using (var ctx = new OrganizationServiceContext(service))
            {
                var qe = new QueryExpression<Test>();
                service.Execute(Arg.Any<OrganizationRequest>()).Returns(new RetrieveMultipleResponse {
                    Results = new ParameterCollection { { "EntityCollection", new EntityCollection { Entities = {
                        new Test ()
                    } } } }  
                });

                var result = qe.RetrieveMultiple(ctx);

                Assert.Single(result.Entities);
            }
        }

        [Fact]
        public void RetrieveMultiple_HydratesOneToManyLinkEntityAsExpected()
        {
            var qe = new QueryExpression<Test>
            {
                LinkEntities = {
                    new LinkEntity<Test>(x => x.TestChilds)
                }
            };
            Guid testId = Guid.NewGuid(), testChildId = Guid.NewGuid();
            var entityCollection = new EntityCollection(new List<Entity>{
                new Test {
                    Id = testId, Name = "Test1", DayOfWeek = DayOfWeek.Monday,
                    Attributes =
                    {
                        { "A.ee_testchildid", new AliasedValue("ee_testchild", "ee_testchildid", testChildId) },
                        { "A.ee_testid", new AliasedValue("ee_testchild", "ee_testid", new EntityReference("ee_test", testId)) }
                    }
                }
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(entityCollection);

            var result = qe.RetrieveMultiple(service);

            var actual = result.Entities.First();
            Assert.Equal(testId, actual.Id);
            Assert.Equal("Test1", actual.Name);
            Assert.Equal(DayOfWeek.Monday, actual.DayOfWeek);
            var subActual = Assert.Single(actual.TestChilds);
            Assert.Equal(testChildId, subActual.Id);
            Assert.Equal(testId, subActual.TestId.Id);
        }

        [Fact]
        public void RetrieveMultiple_HydratesManyToManyLinkEntityAsExpected()
        {
            var qe = new QueryExpression<Test>
            {
                LinkEntities = {
                    new LinkEntity<Test>(x => x.TestManys)
                }
            };
            Guid testId = Guid.NewGuid(), testManyId = Guid.NewGuid();
            var entityCollection = new EntityCollection(new List<Entity>{
                new Test {
                    Id = testId, Name = "Test1", DayOfWeek = DayOfWeek.Monday,
                    Attributes =
                    {
                        { "A.ee_testmanyid", new AliasedValue("ee_testmany", "ee_testmanyid", testManyId) }
                    }
                }
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(entityCollection);

            var result = qe.RetrieveMultiple(service);

            var actual = result.Entities.First();
            Assert.Equal(testId, actual.Id);
            Assert.Equal("Test1", actual.Name);
            Assert.Equal(DayOfWeek.Monday, actual.DayOfWeek);
            var subActual = Assert.Single(actual.TestManys);
            Assert.Equal(testManyId, subActual.Id);
        }

        [Fact]
        public void RetrieveMultiple_HydratesWithLinkEntityAsExpected()
        {
            var qe = new QueryExpression<Test>
            {
                LinkEntities = {
                    new LinkEntity<Test>(x => x.TestManys)
                }
            };
            Guid testId = Guid.NewGuid(), testManyId = Guid.NewGuid();
            var entityCollection = new EntityCollection(new List<Entity>{
                new Test {
                    Id = testId, Name = "Test1", DayOfWeek = DayOfWeek.Monday,
                    Attributes =
                    {
                        { "A.ee_testmanyid", new AliasedValue("ee_testmany", "ee_testmanyid", testManyId) }
                    }
                }
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(entityCollection);

            var result = qe.RetrieveMultiple(service);

            var actual = result.Entities.First();
            Assert.Equal(testId, actual.Id);
            Assert.Equal("Test1", actual.Name);
            Assert.Equal(DayOfWeek.Monday, actual.DayOfWeek);
            var subActual = Assert.Single(actual.TestManys);
            Assert.Equal(testManyId, subActual.Id);
        }

        [Fact]
        public void Retrieve_HydratesSubLinkEntitiesAsExpected()
        {
            var qe = new QueryExpression<Test>
            {
                LinkEntities = {
                    new LinkEntity<Test, Test>(x => x.ParentTest)
                    {
                        LinkEntities =
                        {
                            new LinkEntity<Test>(x => x.TestChilds)
                        }
                    }
                }
            };
            var id = Guid.NewGuid();
            var parentId = Guid.NewGuid();
            var entityCollection = new EntityCollection(new List<Entity>{
                new Test {
                    Id = id, 
                    Name = "Test1", DayOfWeek = DayOfWeek.Monday,
                    Attributes =
                    {
                        { "A.ee_testid", new AliasedValue("ee_test", "ee_testid", parentId) },
                        { "A.ee_name", new AliasedValue("ee_test", "ee_name", "Parent Test") },
                        { "A.ee_dayofweek", new AliasedValue("ee_test", "ee_dayofweek", new OptionSetValue((int)DayOfWeek.Friday)) },
                        { "B.ee_testchildid", new AliasedValue("ee_testchild", "ee_testchildid", Guid.NewGuid()) },
                        { "B.ee_testid", new AliasedValue("ee_testchild", "ee_testid", new EntityReference("ee_test", parentId)) }
                    }
                },
                new Test {
                    Id = id, 
                    Attributes =
                    {
                        { "A.ee_testid", new AliasedValue("ee_test", "ee_testid", parentId) },
                        { "B.ee_testchildid", new AliasedValue("ee_testchild", "ee_testchildid", Guid.NewGuid()) },
                        { "B.ee_testid", new AliasedValue("ee_testchild", "ee_testid", new EntityReference("ee_test", parentId)) }
                    }
                }
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(entityCollection);

            var result = qe.Retrieve(service, id);

            Assert.Equal("Test1", result?.Name);
            Assert.Equal(DayOfWeek.Monday, result?.DayOfWeek);
            var parent = result?.ParentTest;
            Assert.Equal("Parent Test", parent?.Name);
            Assert.Equal(DayOfWeek.Friday, parent?.DayOfWeek);
            Assert.Equal(2, parent?.TestChilds.Count());
        }

        [Fact]
        public void Retrieve_HydratesOneToManyLinkEntityAsExpected()
        {
            var qe = new QueryExpression<Test>
            {
                LinkEntities =
                {
                    new LinkEntity<Test>(x => x.TestChilds)
                }
            };
            var id = Guid.NewGuid();
            var entityCollection = new EntityCollection(new List<Entity>{
                new Test {
                    Id = id,
                    Name = "Test1", DayOfWeek = DayOfWeek.Monday,
                    Attributes =
                    {
                        { "A.ee_testchildid", new AliasedValue("ee_testchild", "ee_testchildid", Guid.NewGuid()) },
                        { "A.ee_testid", new AliasedValue("ee_testchild", "ee_testid", new EntityReference("ee_test", id)) }
                    }
                },
                new Test {
                    Id = id,
                    Name = "Test error", DayOfWeek = DayOfWeek.Tuesday, // ignored
                    Attributes =
                    {
                        { "A.ee_testchildid", new AliasedValue("ee_testchild", "ee_testchildid", Guid.NewGuid()) },
                        { "A.ee_testid", new AliasedValue("ee_testchild", "ee_testid", new EntityReference("ee_test", id)) }
                    }
                }
            });
            service.RetrieveMultiple(Arg.Any<QueryBase>()).Returns(entityCollection);

            var result = qe.Retrieve(service, id);

            Assert.Equal("Test1", result?.Name);
            Assert.Equal(DayOfWeek.Monday, result?.DayOfWeek);
            Assert.Equal(2, result?.TestChilds.Count());
        }
    }
}
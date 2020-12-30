using EarlyXrm.QueryExpressionExtensions.UnitTests.Model;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests
{
    public class EntityCollectionUnitTests
    {
        [Fact]
        public void CastToEntityCollection_AsExpected()
        {
            var ec = new EntityCollection<Test>
            {
                TotalRecordCount = 2,
                Entities =
                {
                    new Test { Name = "one"}, new Test{ Name = "two" }
                }
            };

            var result = (EntityCollection)ec;

            Assert.Equal("ee_test", result.EntityName);
            Assert.Equal(2, result.TotalRecordCount);
        }

        [Fact]
        public void FlattenedModelExplodedAsExpected()
        {
            var id = Guid.NewGuid();
            var ec = new EntityCollection()
            {
                Entities =
                {
                    new Test {
                        Id = id,
                        Name = "one",
                        Attributes =
                        {
                            { "A.ee_testchildid", new AliasedValue("ee_testchild", "ee_testchildid", Guid.NewGuid()) },
                            { "A.ee_name", new AliasedValue("ee_testchild", "ee_name", "My name 1") }
                        }
                    },
                    new Test
                    {
                        Id = id,
                        Name = "one",
                        Attributes =
                        {
                            { "A.ee_testchildid", new AliasedValue("ee_testchild", "ee_testchildid", Guid.NewGuid()) },
                            { "A.ee_name", new AliasedValue("ee_testchild", "ee_name", "My name 2") }
                        }
                    }
                }
            };
            
            var result = new EntityCollection<Test>(ec, new Dictionary<char, string> { { 'A', "ee_Test_TestChilds" } });

            var test = Assert.Single(result.Entities);
            Assert.Equal(2, test.TestChilds.Count());
            Assert.Equal("My name 1", test.TestChilds.First().Name);
            Assert.Equal("My name 2", test.TestChilds.Last().Name);
        }
    }
}
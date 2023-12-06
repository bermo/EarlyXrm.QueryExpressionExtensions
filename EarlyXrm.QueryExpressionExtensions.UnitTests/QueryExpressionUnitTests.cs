using EarlyXrm.QueryExpressionExtensions.UnitTests.Model;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests
{
    public class QueryExpressionUnitTests
    {
        [Fact]
        public void BasicProperties_MappedAsExpected()
        {
            var qe = new QueryExpression<Test>
            {
                TopCount = 1,
                Distinct = true,
                PageInfo = new PagingInfo
                {
                    Count = 2,
                    PageNumber = 3,
                    ReturnTotalRecordCount = true
                },
                ColumnSet = new (x => x.Name)
            };

            var result = (QueryExpression)qe;

            Assert.Equal(1, result.TopCount);
            Assert.True(result.Distinct);
            Assert.Equal(2, result.PageInfo.Count);
            Assert.Equal(3, result.PageInfo.PageNumber);
            Assert.True(result.PageInfo.ReturnTotalRecordCount);
            Assert.Equal(new[] { "ee_name" }, result.ColumnSet.Columns);
        }

        [Fact]
        public void BasicCollectionProperties_MappedAsExpected()
        {
            var qe = new QueryExpression<Test> { 
                Conditions = {
                    ConditionExpression<Test>.In(x => x.DayOfWeek, DayOfWeek.Friday)
                },
                Orders = {
                    new (x => x.Name, OrderType.Ascending)
                }
            };
            
            var result = (QueryExpression)qe;

            var condition = Assert.Single(result.Criteria.Conditions);
            Assert.Equal("ee_dayofweek", condition.AttributeName);
            Assert.Equal(ConditionOperator.In, condition.Operator);
            Assert.Equal(new object[] { (int)DayOfWeek.Friday }, condition.Values);
            var order = Assert.Single(result.Orders);
            Assert.Equal("ee_name", order.AttributeName);
            Assert.Equal(OrderType.Ascending, order.OrderType);
        }

        [Fact]
        public void LinkEntities_MappedAsExpected()
        {
            var qe = new QueryExpression<Test>
            {
                LinkEntities = {
                    new (x => x.TestChilds),
                    new (x => x.TestManys)
                }
            };

            var result = (QueryExpression)qe;

            var firstLinkEntity = result.LinkEntities.First();
            Assert.Equal("ee_testchild", firstLinkEntity.LinkToEntityName);
            var lastLinkEntity = result.LinkEntities.Last();
            Assert.Equal("ee_test_testmanys", lastLinkEntity.LinkToEntityName);
            var lastSubLinkEntity = Assert.Single(lastLinkEntity.LinkEntities);
            Assert.Equal("ee_testmany", lastSubLinkEntity.LinkToEntityName);
        }
    }
}
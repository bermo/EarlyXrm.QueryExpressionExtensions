using EarlyXrm.QueryExpressionExtensions.UnitTests.Model;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests
{
    public class FilterExpressionUnitTests
    {
        [Fact]
        public void LogicalAnd_SetupAsExpected()
        {
            var fe = new FilterExpression<Entity>();

            var result = (FilterExpression)fe;

            result.FilterOperator.Equals(fe.FilterOperator);
        }

        [Fact]
        public void LogicalOr_SeuptAsExpected()
        {
            var fe = new FilterExpression<Entity>(LogicalOperator.Or);

            var result = (FilterExpression)fe;

            result.FilterOperator.Equals(fe.FilterOperator);
        }

        [Fact]
        public void Conditions_SetupAsExpected()
        {
            var id = Guid.NewGuid();
            var fe = new FilterExpression<Test>
            {
                Conditions =
                {
                    new ConditionExpression<Test>(x => x.Id, id),
                    new ConditionExpression<Test>(x => x.Name, ConditionOperator.In, "test1", "test2")
                }
            };

            var result = (FilterExpression)fe;

            var first = result.Conditions.First();
            Assert.Equal("ee_testid", first.AttributeName);
            Assert.Equal(ConditionOperator.Equal, first.Operator);
            Assert.Equal(new object[]{id}, first.Values);
            var last = result.Conditions.Last();
            Assert.Equal("ee_name", last.AttributeName);
            Assert.Equal(ConditionOperator.In, last.Operator);
            Assert.Equal(new object[] { "test1", "test2" }, last.Values);
        }
    }
}
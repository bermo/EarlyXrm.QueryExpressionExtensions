using EarlyXrm.QueryExpressionExtensions.UnitTests.Model;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests
{
    public class ConditionExpressionUnitTests
    {
        [Fact]
        public void ConditionOperator_IsPopulatedAsExpected()
        {
            var ce = new ConditionExpression<Test>(x => x.Name, ConditionOperator.Equal, "Test");

            var result = (ConditionExpression)ce;

            Assert.Equal("ee_name", result.AttributeName);
            Assert.Equal(ConditionOperator.Equal, result.Operator);
            Assert.Equal(new[] { "Test" }, result.Values);
        }

        [Fact]
        public void Constructor_IsPopulatedAsExpected()
        {
            var ce = new ConditionExpression<Test>(x => x.Name, "Test");

            var result = (ConditionExpression)ce;

            Assert.Equal("ee_name", result.AttributeName);
            Assert.Equal(ConditionOperator.Equal, result.Operator);
            Assert.Equal(new[] { "Test" }, result.Values);
        }

        [Fact]
        public void Equal_IsPopulatedAsExpected()
        {
            var ce = ConditionExpression<Test>.Equal(x => x.Name, "Test");

            var result = (ConditionExpression)ce;

            Assert.Equal("ee_name", result.AttributeName);
            Assert.Equal(ConditionOperator.Equal, result.Operator);
            Assert.Equal(new[] { "Test" }, result.Values);
        }

        [Fact]
        public void In_IsPopulatedAsExpected()
        {
            var values = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var ce = ConditionExpression<Test>.In(x => x.Id, values);

            var result = (ConditionExpression)ce;

            Assert.Equal("ee_testid", result.AttributeName);
            Assert.Equal(ConditionOperator.In, result.Operator);
            Assert.Equal(values.Select(x => (object)x), result.Values);
        }

        [Fact]
        public void Null_IsPopulatedAsExpected()
        {
            var ce = ConditionExpression<Test>.Null(x => x.Id);

            var result = (ConditionExpression)ce;

            Assert.Equal("ee_testid", result.AttributeName);
            Assert.Equal(ConditionOperator.Null, result.Operator);
            Assert.Equal(new object[0], result.Values);
        }

        [Fact]
        public void NotNull_IsPopulatedAsExpected()
        {
            var ce = ConditionExpression<Test>.NotNull(x => x.Id);

            var result = (ConditionExpression)ce;

            Assert.Equal("ee_testid", result.AttributeName);
            Assert.Equal(ConditionOperator.NotNull, result.Operator);
            Assert.Equal(new object[0], result.Values);
        }

        [Fact]
        public void EqualEntityReference_IsPopulatedAsExpected()
        {
            var parentId = Guid.NewGuid();
            var ce = ConditionExpression<Test>.Equal(x => x.ParentTestRef, new EntityReference("ee_test", parentId));

            var result = (ConditionExpression)ce;

            Assert.Equal("ee_parenttestid", result.AttributeName);
            Assert.Equal(ConditionOperator.Equal, result.Operator);
            Assert.Equal(new object[] { parentId }, result.Values);
        }

        [Fact]
        public void InEnum_IsPopulatedAsExpected()
        {
            var ce = ConditionExpression<Test>.In(x => x.DayOfWeek, DayOfWeek.Monday, DayOfWeek.Tuesday);

            var result = (ConditionExpression)ce;

            Assert.Equal("ee_dayofweek", result.AttributeName);
            Assert.Equal(ConditionOperator.In, result.Operator);
            Assert.Equal(new object[] { (int)DayOfWeek.Monday, (int)DayOfWeek.Tuesday }, result.Values);
        }
    }
}
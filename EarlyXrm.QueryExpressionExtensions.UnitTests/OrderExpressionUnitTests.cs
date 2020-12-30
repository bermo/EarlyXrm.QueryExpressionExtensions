using EarlyXrm.QueryExpressionExtensions.UnitTests.Model;
using Microsoft.Xrm.Sdk.Query;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests
{
    public class OrderExpressionUnitTests
    {
        [Fact]
        public void AscendingOrderSetAsExpected()
        {
            var oe = new OrderExpression<Test>(x => x.DayOfWeek);

            var result = (OrderExpression)oe;

            Assert.Equal("ee_dayofweek", oe.AttributeName);
            Assert.Equal(OrderType.Ascending, oe.OrderType);
        }

        [Fact]
        public void DescendingOrderSetAsExpected()
        {
            var oe = new OrderExpression<Test>(x => x.DayOfWeek, OrderType.Descending);

            var result = (OrderExpression)oe;

            Assert.Equal("ee_dayofweek", oe.AttributeName);
            Assert.Equal(OrderType.Descending, oe.OrderType);
        }
    }
}
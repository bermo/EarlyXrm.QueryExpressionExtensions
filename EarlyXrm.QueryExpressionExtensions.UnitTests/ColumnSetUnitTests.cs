using EarlyXrm.QueryExpressionExtensions.UnitTests.Model;
using Microsoft.Xrm.Sdk.Query;
using System.Linq;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests
{
    public class ColumnSetUnitTests
    {
        [Fact]
        public void ColumnsAreSetAsExpected()
        {
            var columnSet = new ColumnSet<Test>(x => x.Name);

            var result = (ColumnSet)columnSet;

            Assert.Equal(new[] { "ee_name" }, result.Columns.Cast<string>());
        }
    }
}
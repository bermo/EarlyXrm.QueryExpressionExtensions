using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.ObjectModel;

namespace EarlyXrm.QueryExpressionExtensions
{
    public class FilterExpression<T> where T : Entity
    {
        public static implicit operator FilterExpression(FilterExpression<T> self)
        {
            var val = new FilterExpression
            {
                FilterOperator = self.FilterOperator
            };

            foreach (var filter in self.Filters)
                val.Filters.Add(filter);

            foreach (var condition in self.Conditions)
                val.Conditions.Add(condition);

            return val;
        }

        public FilterExpression(LogicalOperator logicalOperator = LogicalOperator.And)
        {
            FilterOperator = logicalOperator;
        }

        public LogicalOperator FilterOperator { get; set; }

        public Collection<FilterExpression<T>> Filters { get; } = new Collection<FilterExpression<T>>();

        public Collection<ConditionExpression<T>> Conditions { get; set; } = new Collection<ConditionExpression<T>>();
    }
}
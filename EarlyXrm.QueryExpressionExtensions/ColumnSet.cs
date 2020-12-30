using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EarlyXrm.QueryExpressionExtensions
{
    public class ColumnSet<T> where T : Entity
    {
        public static implicit operator ColumnSet(ColumnSet<T> self)
        {
            if (self == null)
                return null;

            return new ColumnSet(self.Columns.ToArray());
        }

        public ColumnSet(params Expression<Func<T, object>>[] columns)
        {
            Columns = columns.Select(x => x.LogicalName());
        }

        public IEnumerable<string> Columns { get; private set; }
    }
}
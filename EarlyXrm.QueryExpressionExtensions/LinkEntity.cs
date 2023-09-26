using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EarlyXrm.QueryExpressionExtensions;

public class LinkEntity<T, U> : LinkEntity<T>, ILinkEntity<T> 
    where T : Entity
    where U : Entity
{
    public static implicit operator LinkEntity(LinkEntity<T, U> self)
    {
        var le = new LinkEntity<T> {
            lambdaExpression = self.lambdaExpression
        };

        le.EntityAlias = self.EntityAlias;
        le.JoinOperator = self.JoinOperator ?? default;
        le.Columns = self.Columns ?? new ColumnSet(true);
        le.LinkCriteria = self.LinkCriteria;

        foreach (var order in self?.Orders ?? new Collection<OrderExpression<U>>())
            le.Orders.Add(order);

        foreach (var linkCondition in self?.LinkConditions ?? new Collection<ConditionExpression<U>>())
            le.LinkCriteria.Conditions.Add(linkCondition);

        foreach (var sublinkentity in self!.LinkEntities)
            le.LinkEntities.Add(sublinkentity);

        return le;
    }

    public new Collection<OrderExpression<U>> Orders { get; set; } = new Collection<OrderExpression<U>>();

    public new ColumnSet<U> Columns { get; set; } = new ColumnSet<U>();

    public new FilterExpression<U> LinkCriteria { get; set; } = new FilterExpression<U>();

    public new Collection<ConditionExpression<U>> LinkConditions { get; set; } = new Collection<ConditionExpression<U>>();

    public new Collection<LinkEntity<U>> LinkEntities { get; set; } = new Collection<LinkEntity<U>>();

    LambdaExpression? ILinkEntity.ParentExpression => lambdaExpression;

    IEnumerable<ILinkEntity> ILinkEntity.LinkQueryExpressions => LinkEntities.Select(x => x as ILinkEntity);

    ColumnSet ILinkEntity.Columns => Columns;

    FilterExpression ILinkEntity.LinkCriteria => LinkCriteria;
    IEnumerable<ConditionExpression> ILinkEntity.LinkConditions => LinkConditions.Select(x => (ConditionExpression)x);
    IEnumerable<OrderExpression> ILinkEntity.Orders => Orders.Select(x => (OrderExpression)x);
    IEnumerable<LinkEntity> ILinkEntity.LinkEntities => LinkEntities.Select(x => (LinkEntity)x);

    public LinkEntity(Expression<Func<T, U?>> expression, JoinOperator? joinOperator = null)
    {
        lambdaExpression = expression;
        JoinOperator = joinOperator;
    }

    public LinkEntity(Expression<Func<T, IEnumerable<U>>> expression, JoinOperator? joinOperator = null)
    {
        lambdaExpression = expression;
        JoinOperator = joinOperator;
    }
}

public class LinkEntity<T> : ILinkEntity<T> 
    where T : Entity
{
    public static implicit operator LinkEntity(LinkEntity<T> self)
    {
        var le = self.lambdaExpression;
        var me = le?.Body as MemberExpression;
        var pi = me?.Member as PropertyInfo;
        var relationship = pi?.GetCustomAttribute<RelationshipSchemaNameAttribute>();

        if (relationship == null) // class may be overriden
        {
            var pType = me?.Expression?.Type;
            var properties = pType?.GetProperties().Where(x => x.Name == pi?.Name);
            var overriden = properties?.FirstOrDefault(x => x.DeclaringType == pType);
            if (overriden != null) pi = overriden;
            relationship = properties?.First(x => x.DeclaringType != pType).GetCustomAttribute<RelationshipSchemaNameAttribute>();
        }

        var parentType = pi?.DeclaringType;
        var parentLogical = parentType?.GetCustomAttribute<EntityLogicalNameAttribute>()?.LogicalName;
        var primaryKey = parentType?.GetProperty("Id")?.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
        var linkproperty = parentType?.GetProperties().Select(x => new { x, att = x.GetCustomAttribute<RelationshipSchemaNameAttribute>() })
                            .Where(x => x.att != null)
                            .FirstOrDefault(x => x.att?.SchemaName == relationship?.SchemaName && (relationship?.PrimaryEntityRole == null || x.att?.PrimaryEntityRole == relationship.PrimaryEntityRole))?.x;
        var childType = linkproperty!.PropertyType.IsGenericType ? linkproperty.PropertyType.GetGenericArguments()[0] : linkproperty.PropertyType;
        var childLogical = childType.GetCustomAttribute<EntityLogicalNameAttribute>()?.LogicalName;

        var linkschemaName = relationship?.SchemaName;
        var parentProp = childType.GetProperties().Select(x => new { x, att = x.GetCustomAttribute<RelationshipSchemaNameAttribute>() })
                            .FirstOrDefault(y => y.att?.SchemaName == linkschemaName && (relationship?.PrimaryEntityRole == null || y.att?.PrimaryEntityRole != relationship.PrimaryEntityRole))?.x;
        var linkAttribute = parentProp?.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;

        LinkEntity topLinkEntity;
        var linkEntity = new LinkEntity
        {
            EntityAlias = self.EntityAlias,
            JoinOperator = self.JoinOperator ?? default(JoinOperator),
            LinkCriteria = self.LinkCriteria,
        };

        if (linkAttribute != null) // one-to-many
        {
            linkEntity.LinkFromEntityName = parentLogical;
            linkEntity.LinkFromAttributeName = primaryKey;
            linkEntity.LinkToEntityName = childLogical;
            linkEntity.LinkToAttributeName = linkAttribute;
            topLinkEntity = linkEntity;
        }
        else
        {
            var childPk = childType.GetProperty("Id")?.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
            linkAttribute = linkproperty.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
            if (linkAttribute != null) // many-to-one
            {
                linkEntity.LinkFromEntityName = parentLogical;
                linkEntity.LinkFromAttributeName = linkAttribute;
                linkEntity.LinkToEntityName = childLogical;
                linkEntity.LinkToAttributeName = childPk;
                topLinkEntity = linkEntity;
            }
            else // ManyToMany
            {
                linkEntity.Columns = null;
                linkEntity.LinkCriteria = null;
                linkEntity.LinkFromEntityName = parentLogical;
                linkEntity.LinkFromAttributeName = primaryKey;

                var attributeProvider = pi?.GetCustomAttribute<AttributeProviderAttribute>();
                if (attributeProvider != null)
                {
                    var type = Type.GetType(attributeProvider.TypeName!);
                    linkschemaName = type?.GetCustomAttribute<EntityLogicalNameAttribute>()?.LogicalName;
                }
                else // try to work out many-to-many name
                {
                    if (linkschemaName!.EndsWith("_association")) // Alot of built-in many-to-many junction entities have an "_association" suffix ...
                    {
                        linkschemaName = linkschemaName.Substring(0, linkschemaName.Length - "_association".Length);
                    }
                    else
                    {
                        var weirdSetup = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase)
                        {
                            { "knowledgearticle_category", "KnowledgeArticleCategory" },
                            { "ChannelAccessProfile_Privilege", "ChannelAccessProfileEntityAccessLevel" },
                            { "contact_subscription_association", "SubscriptionManuallyTrackedObject" },
                            { "serviceplan_appmodule", "ServicePlanAppModules" },
                            { "sample_product_knowledgebaserecord", "msdyn_sample_product_knowledgebaserecord" }
                        };

                        if (weirdSetup.ContainsKey(linkschemaName))
                        {
                            linkschemaName = weirdSetup[linkschemaName];
                        }
                    }
                }

                linkEntity.LinkToEntityName = linkschemaName?.ToLower(); // use RelationshipSchema by convention for custom entities ...

                linkEntity.LinkToAttributeName = primaryKey;

                linkEntity.JoinOperator = Microsoft.Xrm.Sdk.Query.JoinOperator.LeftOuter;

                topLinkEntity = new LinkEntity
                {
                    EntityAlias = linkEntity.EntityAlias,
                    LinkFromEntityName = linkEntity.LinkToEntityName,
                    LinkFromAttributeName = childPk,
                    LinkToEntityName = childLogical,
                    LinkToAttributeName = childPk,
                };

                linkEntity.LinkEntities.Add(topLinkEntity);
                linkEntity.EntityAlias = null;
            }
        }

        var iLinkEntity = self as ILinkEntity<T>;
        if (iLinkEntity != null)
        {
            topLinkEntity.Columns = iLinkEntity.Columns != null ? iLinkEntity.Columns : new ColumnSet(true);
            topLinkEntity.LinkCriteria = iLinkEntity.LinkCriteria;

            foreach (var linkCondition in iLinkEntity?.LinkConditions ?? new Collection<ConditionExpression>())
                topLinkEntity.LinkCriteria.Conditions.Add(linkCondition);

            foreach (var order in iLinkEntity?.Orders ?? new Collection<OrderExpression>())
                topLinkEntity.Orders.Add(order);

            foreach (var subLinkEntity in iLinkEntity!.LinkEntities)
                topLinkEntity.LinkEntities.Add(subLinkEntity);
        }

        return linkEntity;
    }

    internal LinkEntity() { }

    public LinkEntity(Expression<Func<T, Entity>> expression, JoinOperator? joinOperator = null)
    {
        lambdaExpression = expression;
        JoinOperator = joinOperator;
    }

    public LinkEntity(Expression<Func<T, IEnumerable<Entity>>> expression, JoinOperator? joinOperator = null)
    {
        lambdaExpression = expression;
        JoinOperator = joinOperator;
    }

    public Collection<OrderExpression> Orders { get; set; } = new Collection<OrderExpression>();

    public string EntityAlias { get; set; } = "";

    public ColumnSet Columns { get; set; } = new ();

    internal LambdaExpression? lambdaExpression;

    public Collection<LinkEntity> LinkEntities { get; set; } = new Collection<LinkEntity>();

    public FilterExpression LinkCriteria { get; set; } = new FilterExpression();

    public virtual IEnumerable<ConditionExpression> LinkConditions { get; set; } = new Collection<ConditionExpression>();

    LambdaExpression? ILinkEntity.ParentExpression => lambdaExpression;

    public JoinOperator? JoinOperator { get; set; }

    IEnumerable<ILinkEntity> ILinkEntity.LinkQueryExpressions => LinkEntities.Cast<ILinkEntity>();

    IEnumerable<OrderExpression> ILinkEntity.Orders => Orders;

    IEnumerable<LinkEntity> ILinkEntity.LinkEntities => LinkEntities;
}
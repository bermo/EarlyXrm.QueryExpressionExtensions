using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.Collections.Generic;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests.Model
{
    [EntityLogicalName("ee_test")]
    public class Test : Entity
    {
        public Test() : base("ee_test") { }

        [AttributeLogicalName("ee_testid")]
        public new virtual Guid Id
        {
            get => base.Id != default ? base.Id : GetAttributeValue<Guid>("ee_testid");
            set
            {
                base.SetAttributeValue("ee_testid", value);
                base.Id = value;
            }
        }

        [AttributeLogicalName("ee_name")]
        public string Name { 
            get => base.GetAttributeValue<string>("ee_name");
            set => base.SetAttributeValue("ee_name", value); 
        }

        [AttributeLogicalName("ee_dayofweek")]
        public DayOfWeek? DayOfWeek
        {
            get { var val = base.GetAttributeValue<OptionSetValue>("ee_dayofweek"); return val == null ? null : (DayOfWeek?)val.Value; }
            set => base.SetAttributeValue("ee_dayofweek", value == null ? null : new OptionSetValue((int)value));
        }

        [RelationshipSchemaName("ee_Test_TestChilds")]
        public IEnumerable<TestChild> TestChilds
        {
            get => GetRelatedEntities<TestChild>("ee_Test_TestChilds", null);
            set => SetRelatedEntities("ee_Test_TestChilds", null, value);
        }

        [AttributeLogicalName("ee_parenttestid")]
        [RelationshipSchemaName("ee_Test_ParentTest", EntityRole.Referencing)]
        public Test ParentTest
        {
            get => GetRelatedEntity<Test>("ee_Test_ParentTest", EntityRole.Referencing);
            set => SetRelatedEntity("ee_Test_ParentTest", EntityRole.Referencing, value);
        }

        [AttributeLogicalName("ee_parenttestid")]
        public EntityReference ParentTestRef
        {
            get => base.GetAttributeValue<EntityReference>("ee_parenttestid");
            set => base.SetAttributeValue("ee_parenttestid", value);
        }

        [RelationshipSchemaName("ee_Test_ParentTest", EntityRole.Referenced)]
        public IEnumerable<Test> ParentTestTests
        {
            get => GetRelatedEntities<Test>("ee_Test_ParentTest", EntityRole.Referenced);
            set => SetRelatedEntities("ee_Test_ParentTest", EntityRole.Referenced, value);
        }

        [RelationshipSchemaName("ee_Test_TestManys")]
        public IEnumerable<TestMany> TestManys
        {
            get => GetRelatedEntities<TestMany>("ee_Test_TestManys", null);
            set => SetRelatedEntities("ee_Test_TestManys", null, value);
        }
    }
}
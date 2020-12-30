using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests.Model
{
    [EntityLogicalName("ee_testchild")]
    public class TestChild : Entity
    {
        public TestChild() : base("ee_testchild") { }

        [AttributeLogicalName("ee_testchildid")]
        public new virtual Guid Id
        {
            get => base.Id != default ? base.Id : GetAttributeValue<Guid>("ee_testchildid");
            set
            {
                base.SetAttributeValue("ee_testchildid", value);
                base.Id = value;
            }
        }

        [AttributeLogicalName("ee_name")]
        public string Name
        {
            get => base.GetAttributeValue<string>("ee_name");
            set => base.SetAttributeValue("ee_name", value);
        }

        [AttributeLogicalName("ee_testid")]
        public EntityReference TestId
        {
            get => base.GetAttributeValue<EntityReference>("ee_testid");
            set => base.SetAttributeValue("ee_testid", value);
        }

        [AttributeLogicalName("ee_testid")]
        [RelationshipSchemaName("ee_Test_TestChilds")]
        public Test Test
        {
            get => base.GetRelatedEntity<Test>("ee_Test_TestChilds", null);
            set => base.SetRelatedEntity("ee_Test_TestChilds", null, value);
        }
    }
}
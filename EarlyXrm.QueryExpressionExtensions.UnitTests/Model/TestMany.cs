using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.Collections.Generic;

namespace EarlyXrm.QueryExpressionExtensions.UnitTests.Model
{
    [EntityLogicalName("ee_testmany")]
    public class TestMany : Entity
    {
        public TestMany() : base("ee_testmany") { }

        [AttributeLogicalName("ee_testmanyid")]
        public new virtual Guid Id
        {
            get => base.Id != default ? base.Id : GetAttributeValue<Guid>("ee_testmanyid");
            set
            {
                base.SetAttributeValue("ee_testmanyid", value);
                base.Id = value;
            }
        }

        [RelationshipSchemaName("ee_Test_TestManys")]
        public IEnumerable<Test> Tests
        {
            get => GetRelatedEntities<Test>("ee_Test_TestManys", null);
            set => SetRelatedEntities("ee_Test_TestManys", null, value);
        }
    }
}
using EarlyBoundTypes;
using FluentAssertions;
using Microsoft.PowerPlatform.Dataverse.Client;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.IntegrationTests
{
    public class ReportsIntegrationTests : IntegrationTestsBase
    {
        [Fact]
        public void GetReportsAreaReports()
        {
            using var ctx = new ServiceClient(connectionString);

            var reports = new QueryExpression<Report>
            {
                ColumnSet = new ColumnSet<Report>(),
                LinkEntities =
                {
                    new LinkEntity<Report, ReportVisibility>(x => x.Report_ReportVisibilities) {
                        Columns = new ColumnSet<ReportVisibility>(),
                        LinkConditions =
                        {
                            new ConditionExpression<ReportVisibility>(x => x.Visibility, ReportVisibility.Enums.Visibility.ReportsArea)
                        }
                    },
                }
            }.RetrieveMultiple(ctx).Entities;

            reports.Should().HaveCount(3);
        }

        [Fact]
        public void GetReportsWithSubReports()
        {
            using var ctx = new ServiceClient(connectionString);

            var reports = new QueryExpression<Report>
            {
                ColumnSet = new ColumnSet<Report>(x => x.Name),
                LinkEntities =
                {
                    new LinkEntity<Report, ReportLink>(x => x.MainReport_ReportLinks){
                        LinkEntities =
                        {
                            new LinkEntity<ReportLink>(x => x.LinkedReport_Report)
                        }
                    },
                }
            }.RetrieveMultiple(ctx).Entities;

            reports.Should().HaveCount(2);
            var goalsProgress = reports.Should().Contain(x => x.Name == "Progress against goals");
            goalsProgress.Which.MainReport_ReportLinks.Should().Contain(x => x.LinkedReport_Report.Name == "Goal Detail");
            var accountSummary = reports.Should().Contain(x => x.Name == "Account Summary");
            accountSummary.Which.MainReport_ReportLinks.Should().Contain(x => x.LinkedReport_Report.Name == "Account Summary Sub-Report");
        }
    }
}
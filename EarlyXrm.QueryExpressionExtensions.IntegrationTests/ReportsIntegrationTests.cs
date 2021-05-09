using EarlyBoundTypes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Xrm.Tooling.Connector;
using Xunit;

namespace EarlyXrm.QueryExpressionExtensions.IntegrationTests
{
    public class ReportsIntegrationTests
    {
        private string connectionString;

        public ReportsIntegrationTests()
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            connectionString = config.GetConnectionString("Crm");
        }

        [Fact]
        public void GetReportsAreaReports()
        {
            using var ctx = new CrmServiceClient(connectionString);

            var reports = new QueryExpression<Report>
            {
                LinkEntities =
                {
                    new LinkEntity<Report, ReportLink>(x => x.MainReportReportLinks){
                        LinkEntities =
                        {
                            new LinkEntity<ReportLink>(x => x.LinkedReport)
                        }
                    },
                }
            }.RetrieveMultiple(ctx).Entities;

            reports.Should().HaveCount(2);
            var goalsProgress = reports.Should().Contain(x => x.Name == "Progress against goals");
            goalsProgress.Which.MainReportReportLinks.Should().Contain(x => x.LinkedReport.Name == "Goal Detail");
            var accountSummary = reports.Should().Contain(x => x.Name == "Account Summary");
            accountSummary.Which.MainReportReportLinks.Should().Contain(x => x.LinkedReport.Name == "Account Summary Sub-Report");
        }

        [Fact]
        public void GetReportsWithSubReports()
        {
            using var ctx = new CrmServiceClient(connectionString);

            var reports = new QueryExpression<Report>
            {
                LinkEntities =
                {
                    new LinkEntity<Report, ReportLink>(x => x.MainReportReportLinks){
                        LinkEntities =
                        {
                            new LinkEntity<ReportLink>(x => x.LinkedReport)
                        }
                    },
                }
            }.RetrieveMultiple(ctx).Entities;

            reports.Should().HaveCount(2);
            var goalsProgress = reports.Should().Contain(x => x.Name == "Progress against goals");
            goalsProgress.Which.MainReportReportLinks.Should().Contain(x => x.LinkedReport.Name == "Goal Detail");
            var accountSummary = reports.Should().Contain(x => x.Name == "Account Summary");
            accountSummary.Which.MainReportReportLinks.Should().Contain(x => x.LinkedReport.Name == "Account Summary Sub-Report");
        }
    }
}
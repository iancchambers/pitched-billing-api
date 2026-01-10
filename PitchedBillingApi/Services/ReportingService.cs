using Telerik.Reporting;
using Telerik.Reporting.Processing;
using Telerik.Reporting.XmlSerialization;
using PitchedBillingApi.Models;

namespace PitchedBillingApi.Services;

public interface IReportingService
{
    byte[] GenerateInvoicePdf(InvoiceReportData data);
}

public class ReportingService : IReportingService
{
    private readonly ILogger<ReportingService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public ReportingService(ILogger<ReportingService> logger, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _logger = logger;
        _environment = environment;
        _configuration = configuration;
    }

    public byte[] GenerateInvoicePdf(InvoiceReportData data)
    {
        _logger.LogInformation("Generating invoice PDF for invoice {InvoiceNumber}", data.InvoiceNumber);

        // Load TRDX template
        var reportPath = Path.Combine(_environment.ContentRootPath, "Reports", "PitchedInvoice.trdx");

        if (!File.Exists(reportPath))
        {
            throw new FileNotFoundException($"Report template not found: {reportPath}");
        }

        Telerik.Reporting.Report report;
        using (var fs = new FileStream(reportPath, FileMode.Open, FileAccess.Read))
        {
            var xmlSerializer = new ReportXmlSerializer();
            report = (Telerik.Reporting.Report)xmlSerializer.Deserialize(fs);
        }

        // Override connection strings for main report and all subreports
        var connectionString = _configuration["database-connection"];
        if (!string.IsNullOrEmpty(connectionString))
        {
            OverrideConnectionStrings(report, connectionString);
        }
        else
        {
            _logger.LogWarning("No connection string found in configuration under 'database-connection' key");
        }

        // Pass InvoiceId as a parameter to match the report parameter name
        var invoiceIdString = data.InvoiceId.ToString();

        var reportSource = new InstanceReportSource
        {
            ReportDocument = report,
            Parameters =
            {
                { "InvoiceId", invoiceIdString }
            }
        };

        // Render to PDF
        var reportProcessor = new ReportProcessor();
        var renderingResult = reportProcessor.RenderReport("PDF", reportSource, null);

        if (renderingResult.Errors.Length > 0)
        {
            foreach (var error in renderingResult.Errors)
            {
                _logger.LogError("Report rendering error: {Error}", error);
            }
            throw new InvalidOperationException("Failed to render invoice PDF");
        }

        _logger.LogInformation("Invoice PDF generated successfully, size: {Size} bytes", renderingResult.DocumentBytes.Length);

        return renderingResult.DocumentBytes;
    }

    private void OverrideConnectionStrings(Telerik.Reporting.Report report, string connectionString)
    {
        // Override data sources in the main report
        OverrideReportDataSources(report, connectionString);

        // Find all SubReport items recursively and override their connection strings
        var subReports = report.Items.Find(typeof(Telerik.Reporting.SubReport), true);

        foreach (Telerik.Reporting.SubReport subReport in subReports)
        {
            if (subReport.ReportSource is InstanceReportSource instanceSource)
            {
                var nestedReport = instanceSource.ReportDocument as Telerik.Reporting.Report;
                if (nestedReport != null)
                {
                    OverrideReportDataSources(nestedReport, connectionString);
                }
            }
            else if (subReport.ReportSource is UriReportSource uriSource)
            {
                // Load the subreport from URI and override its connection strings
                var subreportPath = Path.Combine(_environment.ContentRootPath, "Reports", uriSource.Uri);
                if (File.Exists(subreportPath))
                {
                    using var fs = new FileStream(subreportPath, FileMode.Open, FileAccess.Read);
                    var xmlSerializer = new ReportXmlSerializer();
                    var nestedReport = xmlSerializer.Deserialize(fs) as Telerik.Reporting.Report;
                    if (nestedReport != null)
                    {
                        OverrideReportDataSources(nestedReport, connectionString);

                        // Create new InstanceReportSource and preserve parameters from UriReportSource
                        var newInstanceSource = new InstanceReportSource { ReportDocument = nestedReport };

                        // Copy parameters from UriReportSource to new InstanceReportSource
                        if (uriSource.Parameters != null && uriSource.Parameters.Count > 0)
                        {
                            foreach (var param in uriSource.Parameters)
                            {
                                newInstanceSource.Parameters.Add(param.Name, param.Value);
                            }
                        }

                        subReport.ReportSource = newInstanceSource;
                    }
                }
                else
                {
                    _logger.LogWarning("Subreport file not found: {Path}", subreportPath);
                }
            }
        }
    }

    private void OverrideReportDataSources(Telerik.Reporting.Report report, string connectionString)
    {
        // Use reflection to access the report's internal data sources
        var dataSourcesProperty = report.GetType().GetProperty("DataSources",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (dataSourcesProperty != null)
        {
            var dataSources = dataSourcesProperty.GetValue(report) as System.Collections.IEnumerable;

            if (dataSources != null)
            {
                foreach (var dataSource in dataSources)
                {
                    if (dataSource is Telerik.Reporting.SqlDataSource sqlDataSource)
                    {
                        sqlDataSource.ConnectionString = connectionString;
                    }
                }
            }
        }

        // Also check report-level DataSource property (if report uses it directly)
        if (report.DataSource is Telerik.Reporting.SqlDataSource reportSqlDataSource)
        {
            reportSqlDataSource.ConnectionString = connectionString;
        }

        // Handle data sources on individual data-bound items (Tables, Lists, etc.)
        var dataItems = report.Items.Find(typeof(Telerik.Reporting.IDataItem), true);

        foreach (var item in dataItems)
        {
            if (item is Telerik.Reporting.IDataItem dataItem && dataItem.DataSource is Telerik.Reporting.SqlDataSource itemSqlDataSource)
            {
                itemSqlDataSource.ConnectionString = connectionString;
            }
        }
    }
}

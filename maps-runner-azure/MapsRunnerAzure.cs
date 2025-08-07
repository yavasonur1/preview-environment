using System;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Core;
using Azure.Identity;

namespace AzureFunctionApp
{
    public class MapsRunnerAzure
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly TokenCredential _credential;

        public MapsRunnerAzure(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MapsRunnerAzure>();
            _httpClient = new HttpClient();
            _credential = new DefaultAzureCredential();
        }

        [Function("MapsRunnerAzure")]
        public async Task RunAsync([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"MapsRunnerAzure executed at: {DateTime.Now}");

            try
            {
                string dbServer = "74.234.169.223";
                string dbName = "PreviewEnvironmentDB";
                string dbUser = "previewenvuser";
                string dbPassword = "Onur123456789";

                string connectionString = $"Server=tcp:{dbServer},1433;Initial Catalog={dbName};Persist Security Info=False;User ID={dbUser};Password={dbPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;";

                int runId;

                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    var selectCmd = new SqlCommand("SELECT TOP 1 RunId FROM engine.AlgoRuns WHERE Status = 0 ORDER BY RunId", conn);
                    var runIdObj = await selectCmd.ExecuteScalarAsync();

                    if (runIdObj == null)
                    {
                        _logger.LogInformation("There is no new run.");
                        return;
                    }

                    runId = (int)runIdObj;
                    _logger.LogInformation($"Processing RunId: {runId}");

                    var updateCmd = new SqlCommand("UPDATE engine.AlgoRuns SET Status = 6 WHERE RunId = @runId", conn);
                    updateCmd.Parameters.AddWithValue("@runId", runId);
                    await updateCmd.ExecuteNonQueryAsync();
                }

                try
                {
                    string subscriptionId = "90b891f0-79da-4298-9f6a-a955ef06c8f8";
                    string resourceGroup = "acme-dev-rg";
                    string jobName = "preview-env-container-job";
                    string apiVersion = "2023-08-07";

                    _logger.LogInformation($"SubscriptionId: {subscriptionId}");
                    _logger.LogInformation($"ResourceGroup: {resourceGroup}");
                    _logger.LogInformation($"JobName: {jobName}");

                    string jobUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.App/jobs/{jobName}/start?api-version={apiVersion}";

                    // Access token al
                    var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
                    var accessToken = await _credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

                    var payload = new
                    {
                        properties = new
                        {
                            configuration = new
                            {
                                container = new
                                {
                                    args = new[] { "dotnet", "Maps.Runner.dll", "--run-id", runId.ToString(), "--customer-name", "integrationtest", "--environment-type", "preview" }
                                }
                            }
                        }
                    };

                    var json = JsonSerializer.Serialize(payload);
                    var request = new HttpRequestMessage(HttpMethod.Post, jobUrl);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation($"Job started successfully for RunId: {runId}");
                    }
                    else
                    {
                        var err = await response.Content.ReadAsStringAsync();
                        _logger.LogError($"Job start failed. Status: {response.StatusCode}, Error: {err}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception while starting container job: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"General exception: {ex.Message}");
            }
        }
    }
}

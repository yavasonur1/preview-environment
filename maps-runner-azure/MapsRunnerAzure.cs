using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace AzureFunctionApp
{
    public class MapsRunnerAzure
    {
        private readonly ILogger _logger;

        public MapsRunnerAzure(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MapsRunnerAzure>();
        }

        [Function("MapsRunnerAzure")]
        public async Task RunAsync([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"MapsRunnerAzure executed at: {DateTime.Now}");

            try
            {
                string keyVaultUrl = "https://your-keyvault-name.vault.azure.net/";

                var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

                string dbServer = (await client.GetSecretAsync("DbServer")).Value.Value;
                string dbName = (await client.GetSecretAsync("DbName")).Value.Value;
                string dbUser = (await client.GetSecretAsync("DbUser")).Value.Value;
                string dbPassword = (await client.GetSecretAsync("DbPassword")).Value.Value;

                string connectionString = $"Server=tcp:{dbServer},1433;Initial Catalog={dbName};Persist Security Info=False;User ID={dbUser};Password={dbPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

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

                    int runId = (int)runIdObj;
                    _logger.LogInformation($"Processed RunId: {runId}");

                    var updateCmd = new SqlCommand("UPDATE engine.AlgoRuns SET Status = 6 WHERE RunId = @runId", conn);
                    updateCmd.Parameters.AddWithValue("@runId", runId);
                    await updateCmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
            }
        }
    }
}

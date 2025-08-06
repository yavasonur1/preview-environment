using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.ContainerInstance.Models;

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
                // Key Vault erişimi
                /*string keyVaultUrl = "https://your-keyvault-name.vault.azure.net/";
                var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

                string dbServer = (await client.GetSecretAsync("DbServer")).Value.Value;
                string dbName = (await client.GetSecretAsync("DbName")).Value.Value;
                string dbUser = (await client.GetSecretAsync("DbUser")).Value.Value;
                string dbPassword = (await client.GetSecretAsync("DbPassword")).Value.Value;*/

                string dbServer = "74.234.169.223,1433";
                string dbName = "PreviewEnvironmentDB";
                string dbUser = "previewenvuser";
                string dbPassword = "Onur123456789";

                string connectionString = $"Server=tcp:{dbServer},1433;Initial Catalog={dbName};Persist Security Info=False;User ID={dbUser};Password={dbPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

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

                // ACI başlat
                string subscriptionId = Environment.GetEnvironmentVariable("90b891f0-79da-4298-9f6a-a955ef06c8f8");
                string resourceGroupName = "acme-dev-rg";
                string containerGroupName = $"run-{runId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                string containerName = "preview-env-container-instance";
                string location = "westeurope";
                string acrServer = "inventpreviewenv.azurecr.io";
                string imageName = "mapsunified-preview-repository";
                string acrImage = $"{acrServer}/{imageName}:latest";

                var credential = new DefaultAzureCredential();
                var armClient = new ArmClient(credential, subscriptionId);

                // Resource Group referansı al
                var resourceGroup = armClient.GetResourceGroupResource(
                    ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

                // ACI tanımı
                var containerGroupData = new ContainerGroupData(location)
                {
                    OsType = ContainerGroupOsType.Linux,
                    RestartPolicy = ContainerGroupRestartPolicy.Never,
                    Containers =
                    {
                        new ContainerInstanceContainer(containerName, acrImage, new ContainerResourceRequirements
                        {
                            Requests = new ContainerResourceRequestsContent(1.0, 1.5)
                        })
                        {
                            Command =
                            {
                                "dotnet", "Maps.Runner.dll",
                                "--run-id", runId.ToString(),
                                "--customer-name", "integrationtest",
                                "--environment-type", "preview"
                            }
                        }
                    },
                    // ImageRegistryCredentials kısmı yok çünkü MSI ile erişim sağlıyoruz
                    Identity = new ContainerGroupManagedServiceIdentity(ManagedServiceIdentityType.UserAssigned)
                    {
                        UserAssignedIdentities =
                        {
                            { new ResourceIdentifier("/subscriptions/90b891f0-79da-4298-9f6a-a955ef06c8f8/resourceGroups/acme-dev-rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/preview-env-managed-identity"), new UserAssignedIdentity() }
                        }
                    }
                };

                // ACI oluştur
                await resourceGroup.GetContainerGroups()
                    .CreateOrUpdateAsync(WaitUntil.Completed, containerGroupName, containerGroupData);

                _logger.LogInformation($"ACI created: {containerGroupName} for runId {runId}");

            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
            }
        }
    }
}

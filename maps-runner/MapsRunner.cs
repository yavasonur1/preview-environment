using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json.Linq;
using System.Data.SqlClient;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MapsRunner
{
    public class Function
    {
        public async Task FunctionHandler(object input, ILambdaContext context)
        {
            string secretName = "preview-env-user-secret";
            var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.EUWest1);
            var request = new GetSecretValueRequest { SecretId = secretName };
            var response = await client.GetSecretValueAsync(request);
            var secret = JObject.Parse(response.SecretString);

            string username = secret["username"].ToString();
            string password = secret["password"].ToString();

            string server = "54.171.82.24";
            string database = "PreviewEnvironmentDB";

            string connStr = $"Server={server};Database={database};User Id={username};Password={password};";

            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                var selectCmd = new SqlCommand("SELECT TOP 1 RunId FROM engine.AlgoRuns WHERE Status = 0 ORDER BY RunId", conn);
                var runIdObj = await selectCmd.ExecuteScalarAsync();

                if (runIdObj == null)
                {
                    context.Logger.LogLine("There is no new run..");
                    return;
                }

                int runId = (int)runIdObj;
                context.Logger.LogLine($"Processed RunId: {runId}");

                var updateCmd = new SqlCommand("UPDATE engine.AlgoRuns SET Status = 6 WHERE RunId = @runId", conn);
                updateCmd.Parameters.AddWithValue("@runId", runId);
                await updateCmd.ExecuteNonQueryAsync();
            }
        }
    }
}

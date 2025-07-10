using System;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json.Linq;
using System.Data.SqlClient;

// Assembly attribute for Lambda runtime
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
            var response = client.GetSecretValueAsync(request).Result;
            var secret = JObject.Parse(response.SecretString);

            string username = secret["username"].ToString();
            string password = secret["password"].ToString();

            string server = "54.171.82.24";
            string database = "PreviewEnvironmentDB";

            string connStr = $"Server={server};Database={database};User Id={username};Password={password};";

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                var selectCmd = new SqlCommand("SELECT TOP 1 RunId FROM engine.AlgoRuns WHERE Status = 0 ORDER BY RunId", conn);
                var runId = selectCmd.ExecuteScalar();

                if (runId == null)
                {
                    context.Logger.LogLine("There is no new run..");
                    return null;
                }

                int runId = (int)runIdObj;
                context.Logger.LogLine($"Processed RunId: {runId}");

                var updateCmd = new SqlCommand("UPDATE engine.AlgoRuns SET Status = 6 WHERE RunId = @runId", conn);
                updateCmd.Parameters.AddWithValue("@runId", runId);
                updateCmd.ExecuteNonQuery();
            }
        }
    }
}

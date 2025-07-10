using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
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
            string region = "eu-west-1";

            var client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.EUWest1);

            var request = new GetSecretValueRequest
            {
                SecretId = secretName
            };

            var response = await client.GetSecretValueAsync(request);
            var secret = JObject.Parse(response.SecretString);

            string username = secret["username"].ToString();
            string password = secret["password"].ToString();

            string server = "54.171.82.24";
            string database = "PreviewEnvironmentDB";

            string connStr = $"Server={server};Database={database};User Id={username};Password={password};";

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT * FROM Users", conn);
                var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    Console.WriteLine($"{reader[0]}");
                    context.Logger.LogLine($"{reader[0]}");
                }
            }
        }
    }
}

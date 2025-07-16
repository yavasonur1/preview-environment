using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json.Linq;
using System.Data.SqlClient;
using ECSTag = Amazon.ECS.Model.Tag;
using SecretsTag = Amazon.SecretsManager.Model.Tag;
using Task = System.Threading.Tasks.Task;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MapsRunner
{
    public class Function
    {
        public async Task FunctionHandler(object input, ILambdaContext context)
        {
            string secretName = "preview-env-user-secret";
            var secretsClient = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.EUWest1);
            var secretsRequest = new GetSecretValueRequest { SecretId = secretName };
            var secretsResponse = await secretsClient.GetSecretValueAsync(secretsRequest);
            var secret = JObject.Parse(secretsResponse.SecretString);

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

                #region Trigger Ecs Task

                var ecsClient = new AmazonECSClient(Amazon.RegionEndpoint.EUWest1);

                string STARTED_BY = "Preview-Env";
                string CUSTOMER_NAME = "IntegrationTest";
                string DB_ENV_TYPE = "preview";
                string CONTAINER = "mapsunified";
                string TASK_DEF = "PreviewEnvRunner";
                string CLUSTER = "acme-dev-cluster";

                var ecsRequest = new RunTaskRequest
                {
                    Cluster = CLUSTER,
                    TaskDefinition = TASK_DEF,
                    LaunchType = LaunchType.FARGATE,
                    NetworkConfiguration = new NetworkConfiguration
                    {
                        AwsvpcConfiguration = new AwsVpcConfiguration
                        {
                            Subnets = new List<string>
                            {
                                "subnet-0ac29334341b00a18"
                            },
                            SecurityGroups = new List<string>
                            {
                                "sg-0ab204d9f4f167e3e"
                            }
                        }
                    },
                    Overrides = new TaskOverride
                    {
                        ContainerOverrides = new List<ContainerOverride>
                        {
                            new ContainerOverride
                            {
                                Name = CONTAINER,
                                Command = new List<string>
                                {
                                    "dotnet",
                                    "Maps.Runner.dll",
                                    "--run-id", runId.ToString(),
                                    "--customer-name", CUSTOMER_NAME,
                                    "--environment-type", DB_ENV_TYPE
                                },
                                Environment = new List<Amazon.ECS.Model.KeyValuePair>
                                {
                                    new Amazon.ECS.Model.KeyValuePair
                                    {
                                        Name = "TZ",
                                        Value = "Europe/Istanbul"
                                    }
                                }
                            }
                        }
                    },
                    StartedBy = STARTED_BY,
                    Tags = new List<ECSTag>
                    {
                        new ECSTag { Key = "StartedBy", Value = STARTED_BY },
                        new ECSTag { Key = "Customer", Value = "Invent" },
                        new ECSTag { Key = "Environment", Value = "Development" },
                        new ECSTag { Key = "Project", Value = "MapsUnified" },
                        new ECSTag { Key = "ServerType", Value = "ECS" }
                    }
                };

                var ecsResponse = await ecsClient.RunTaskAsync(ecsRequest);

                if (ecsResponse.Tasks.Count > 0)
                {
                    var taskArn = ecsResponse.Tasks[0].TaskArn.Split('/')[^1];

                    var ecsLogLink = $"https://eu-west-1.console.aws.amazon.com/ecs/v2/clusters/{CLUSTER}/tasks/{taskArn}/logs?region=eu-west-1";
                    var cloudwatchLink = $"https://eu-west-1.console.aws.amazon.com/cloudwatch/home?region=eu-west-1#logEventViewer:group=/ecs/{CONTAINER};stream=ecs/{CONTAINER}/{taskArn}";

                    context.Logger.LogLine($"ECS Logs => {ecsLogLink}");
                    context.Logger.LogLine($"Cloudwatch Logs => {cloudwatchLink}");
                }
                else
                {
                    context.Logger.LogLine("No ECS task was started.");
                }

                #endregion
            }
        }
    }
}

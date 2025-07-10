using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.Lambda.Core;
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
            string username = "SA";
            string password = "Onur123.";

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
                    context.Logger.LogLine($"{reader[0]}");
                }
            }
        }
    }
}

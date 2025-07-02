import boto3
import pprint


def lambda_handler(event, context):
    STARTED_BY = event.get('started_by', 'default-user')
    RUN_ID = event.get('run_id', '')
    CUSTOMER_NAME = event.get('customer_name', '')
    DB_ENV_TYPE = event.get('db_env_type', 'test')

    CONTAINER = 'mapsunified-preview'
    TASK_DEF = 'mapsunified-preview'
    REGION_NAME = 'eu-west-1'

    client = boto3.client("ecs", region_name=REGION_NAME)

    response = client.run_task(
        cluster="InventCustomCommands",
        taskDefinition=TASK_DEF,
        launchType="FARGATE",
        networkConfiguration={
            "awsvpcConfiguration": {
                "subnets": [
                    "subnet-026e0512a3c831f7a",
                    "subnet-08bed6b2f1dd3af7c",
                    "subnet-0eccfca44e2729c83"
                ],
            }
        },
        overrides={
            "containerOverrides": [{
                "name": CONTAINER,
                "command": [
                    "dotnet",
                    "Maps.Runner.dll",
                    "--run-id", RUN_ID,
                    "--customer-name", CUSTOMER_NAME,
                    "--environment-type", DB_ENV_TYPE,
                ],
                "environment": [
                    {"name": "TZ", "value": "Europe/Istanbul"},
                ],
            }],
        },
        startedBy=STARTED_BY,
        tags=[
            {"key": "StartedBy", "value": STARTED_BY},
            {"key": "Customer", "value": CUSTOMER_NAME},
            {"key": "Environment", "value": "Development"},
            {"key": "Project", "value": "MapsUnified"},
            {"key": "ServerType", "value": "ECS"},
        ],
    )

    pprint.pprint(response)

    return {
        "statusCode": 200,
        "body": response
    }

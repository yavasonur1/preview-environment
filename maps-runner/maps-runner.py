import json
import boto3
import pyodbc


def lambda_handler(event, context):
    secret_name = "preview-env-user-secret"
    region_name = "eu-west-1"
    session = boto3.session.Session()
    client = session.client(service_name='secretsmanager', region_name=region_name)
    get_secret_value_response = client.get_secret_value(SecretId=secret_name)
    secret = json.loads(get_secret_value_response['SecretString'])

    username = secret['username']
    password = secret['password']

    server = '54.171.82.24'
    database = 'PreviewEnvironmentDB'

    # pyodbc connection string
    conn_str = f'DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={server};DATABASE={database};UID={username};PWD={password}'

    with pyodbc.connect(conn_str) as conn:
        cursor = conn.cursor()
        cursor.execute("SELECT * FROM Users")
        rows = cursor.fetchall()
        for row in rows:
            print(row)

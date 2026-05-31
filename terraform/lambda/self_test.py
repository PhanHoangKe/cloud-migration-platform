import json
import os
import uuid
from datetime import datetime, timezone

import boto3


def response(status_code, body):
    return {
        "statusCode": status_code,
        "headers": {
            "Content-Type": "application/json",
            "Access-Control-Allow-Origin": "*"
        },
        "body": json.dumps(body)
    }


def lambda_handler(event, context):
    region = os.environ.get("AWS_REGION", "ap-southeast-1")
    bucket_name = os.environ.get("MIGRATION_BUCKET_NAME")
    table_name = os.environ.get("MIGRATION_LOGS_TABLE")

    endpoint_host = os.environ.get("LOCALSTACK_HOSTNAME")
    endpoint_url = f"http://{endpoint_host}:4566" if endpoint_host else "http://localhost:4566"

    checks = {
        "s3_bucket": "UNKNOWN",
        "dynamodb_table": "UNKNOWN",
        "write_log": "UNKNOWN"
    }

    try:
        s3 = boto3.client(
            "s3",
            endpoint_url=endpoint_url,
            region_name=region,
            aws_access_key_id="test",
            aws_secret_access_key="test"
        )

        dynamodb = boto3.resource(
            "dynamodb",
            endpoint_url=endpoint_url,
            region_name=region,
            aws_access_key_id="test",
            aws_secret_access_key="test"
        )

        # Check S3 bucket
        s3.head_bucket(Bucket=bucket_name)
        checks["s3_bucket"] = "PASSED"

        # Check DynamoDB table
        table = dynamodb.Table(table_name)
        table.load()
        checks["dynamodb_table"] = "PASSED"

        # Write self-test log
        migration_id = f"SELF_TEST_{uuid.uuid4()}"
        table.put_item(
            Item={
                "MigrationId": migration_id,
                "Status": "SELF_TEST_PASSED",
                "Step": "POST_MIGRATION_SELF_TEST",
                "Message": "Post-migration self-test completed successfully",
                "CreatedAt": datetime.now(timezone.utc).isoformat()
            }
        )
        checks["write_log"] = "PASSED"

        return response(200, {
            "success": True,
            "message": "Post-migration self-test completed successfully",
            "migrationId": migration_id,
            "checks": checks
        })

    except Exception as ex:
        return response(500, {
            "success": False,
            "message": str(ex),
            "checks": checks
        })
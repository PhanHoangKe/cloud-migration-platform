terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }

    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.4"
    }
  }
}

provider "aws" {
  access_key = "test"
  secret_key = "test"
  region     = "ap-southeast-1"

  s3_use_path_style           = true
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true

  endpoints {
    apigateway   = "http://localhost:4566"
    apigatewayv2 = "http://localhost:4566"
    cloudwatch   = "http://localhost:4566"
    dynamodb     = "http://localhost:4566"
    ec2          = "http://localhost:4566"
    iam          = "http://localhost:4566"
    lambda       = "http://localhost:4566"
    logs         = "http://localhost:4566"
    s3           = "http://localhost:4566"
    sts          = "http://localhost:4566"
  }
}
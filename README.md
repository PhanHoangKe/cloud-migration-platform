# Cloud Migration & Modernization Platform

## 1. Giới thiệu

Đây là đồ án môn học Điện toán đám mây, mô phỏng quá trình chuyển đổi một ứng dụng ASP.NET Core 9 + SQL Server từ môi trường On-premise lên môi trường Cloud giả lập bằng LocalStack.

Hệ thống sử dụng Terraform để tự động hóa hạ tầng và LocalStack để mô phỏng các dịch vụ AWS.

## 2. Mục tiêu

- Mô phỏng quy trình migration ứng dụng từ On-premise lên Cloud.
- Triển khai hạ tầng bằng Terraform.
- Sử dụng LocalStack thay cho AWS thật.
- Tích hợp các dịch vụ cloud như S3, DynamoDB, Lambda, API Gateway và IAM.
- Xây dựng dashboard theo dõi trạng thái migration.
- Cung cấp demo apply/destroy Terraform và demo sản phẩm.

## 3. Công nghệ sử dụng

- ASP.NET Core 9
- SQL Server
- Docker
- LocalStack
- Terraform
- AWS CLI
- S3
- DynamoDB
- Lambda
- API Gateway
- IAM
- Bootstrap 5
- Chart.js

## 4. Kiến trúc tổng quan

Ứng dụng gồm các thành phần chính:

- On-premise Application: ứng dụng ASP.NET Core 9 + SQL Server.
- Migration Dashboard: giao diện quản lý và theo dõi migration.
- S3: lưu trữ file backup và dữ liệu tĩnh.
- DynamoDB: lưu migration logs.
- Lambda: thực hiện self-test sau migration.
- API Gateway: cung cấp endpoint kiểm tra trạng thái.
- Terraform: tự động hóa toàn bộ hạ tầng.

## 5. Cấu trúc thư mục

```text
cloud-migration-platform/
├── app/
│   └── MigrationDashboard/
├── terraform/
│   ├── modules/
│   └── lambda/
├── docs/
│   ├── images/
│   └── report/
├── scripts/
├── demo/
└── README.md
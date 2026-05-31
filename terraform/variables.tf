variable "project_name" {
  description = "Project name used for naming cloud resources"
  type        = string
  default     = "cloud-migration"
}

variable "aws_region" {
  description = "AWS region simulated by LocalStack"
  type        = string
  default     = "ap-southeast-1"
}

variable "student_code" {
  description = "Student code or unique suffix for resource names"
  type        = string
  default     = "kedep"
}
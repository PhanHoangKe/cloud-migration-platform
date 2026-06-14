# =========================
# VPC
# =========================
resource "aws_vpc" "main" {
  cidr_block           = "10.10.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = {
    Name    = "${var.project_name}-vpc"
    Project = var.project_name
  }
}

# =========================
# Public Subnet
# =========================
resource "aws_subnet" "public" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.10.1.0/24"
  availability_zone       = "${var.aws_region}a"
  map_public_ip_on_launch = true

  tags = {
    Name    = "${var.project_name}-public-subnet"
    Project = var.project_name
    Type    = "public"
  }
}

# =========================
# Private Subnet
# =========================
resource "aws_subnet" "private" {
  vpc_id            = aws_vpc.main.id
  cidr_block        = "10.10.2.0/24"
  availability_zone = "${var.aws_region}b"

  tags = {
    Name    = "${var.project_name}-private-subnet"
    Project = var.project_name
    Type    = "private"
  }
}

# =========================
# Internet Gateway
# =========================
resource "aws_internet_gateway" "igw" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name    = "${var.project_name}-igw"
    Project = var.project_name
  }
}

# =========================
# Public Route Table
# =========================
resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name    = "${var.project_name}-public-rt"
    Project = var.project_name
  }
}

resource "aws_route" "public_internet_access" {
  route_table_id         = aws_route_table.public.id
  destination_cidr_block = "0.0.0.0/0"
  gateway_id             = aws_internet_gateway.igw.id
}

resource "aws_route_table_association" "public_assoc" {
  subnet_id      = aws_subnet.public.id
  route_table_id = aws_route_table.public.id
}

# ==========================================
# VPN Site-to-Site (On-Premise to AWS VPC)
# ==========================================

# 1. Customer Gateway (Represents local On-Premise Router)
resource "aws_customer_gateway" "on_prem_cgw" {
  bgp_asn    = 65000
  ip_address = "1.2.3.4" # Simulated On-Premise Public IP
  type       = "ipsec.1"

  tags = {
    Name    = "${var.project_name}-on-prem-cgw"
    Project = var.project_name
  }
}

# 2. Virtual Private Gateway (Attached to AWS VPC)
resource "aws_vpn_gateway" "vpn_gw" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name    = "${var.project_name}-vpn-gw"
    Project = var.project_name
  }
}

# 3. VPN Connection (IPSec Tunnel)
resource "aws_vpn_connection" "site_to_site" {
  vpn_gateway_id      = aws_vpn_gateway.vpn_gw.id
  customer_gateway_id = aws_customer_gateway.on_prem_cgw.id
  type                = "ipsec.1"
  static_routes_only  = true

  tags = {
    Name    = "${var.project_name}-vpn-connection"
    Project = var.project_name
  }
}
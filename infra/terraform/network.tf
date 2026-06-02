###############################################################################
# VPC + public/private subnets across N AZs.
#
# Layout (per AZ):
#   public/<az>   — ALB lives here. Has IGW route.
#   private/<az>  — ECS tasks + RDS live here. Egress via NAT.
#
# Cost note: NAT Gateway is ~$32/mo per AZ. We use ONE NAT in the first AZ
# and route both private subnets through it. If that AZ fails, outbound
# internet from the other AZ stops (inbound ALB→ECS still works). For
# stricter HA, add a second NAT per AZ — doubles NAT cost.
###############################################################################

resource "aws_vpc" "main" {
  cidr_block           = var.vpc_cidr
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = { Name = "${local.name}-vpc" }
}

resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id
  tags   = { Name = "${local.name}-igw" }
}

# ── Subnets ────────────────────────────────────────────────────────────────
resource "aws_subnet" "public" {
  count                   = var.az_count
  vpc_id                  = aws_vpc.main.id
  availability_zone       = data.aws_availability_zones.available.names[count.index]
  cidr_block              = cidrsubnet(var.vpc_cidr, 8, count.index)              # 10.40.0.0/24, 10.40.1.0/24, ...
  map_public_ip_on_launch = true

  tags = {
    Name = "${local.name}-public-${data.aws_availability_zones.available.names[count.index]}"
    Tier = "public"
  }
}

resource "aws_subnet" "private" {
  count             = var.az_count
  vpc_id            = aws_vpc.main.id
  availability_zone = data.aws_availability_zones.available.names[count.index]
  cidr_block        = cidrsubnet(var.vpc_cidr, 8, count.index + 10)               # 10.40.10.0/24, 10.40.11.0/24, ...

  tags = {
    Name = "${local.name}-private-${data.aws_availability_zones.available.names[count.index]}"
    Tier = "private"
  }
}

# ── NAT (single, in first public subnet) ───────────────────────────────────
resource "aws_eip" "nat" {
  domain     = "vpc"
  depends_on = [aws_internet_gateway.main]
  tags       = { Name = "${local.name}-nat-eip" }
}

resource "aws_nat_gateway" "main" {
  allocation_id = aws_eip.nat.id
  subnet_id     = aws_subnet.public[0].id
  tags          = { Name = "${local.name}-nat" }
  depends_on    = [aws_internet_gateway.main]
}

# ── Route tables ───────────────────────────────────────────────────────────
resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = { Name = "${local.name}-public-rt" }
}

resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.main.id
  }

  tags = { Name = "${local.name}-private-rt" }
}

resource "aws_route_table_association" "public" {
  count          = var.az_count
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "private" {
  count          = var.az_count
  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private.id
}

# ── VPC endpoint for S3 (free, avoids NAT bandwidth for S3 calls) ──────────
# Your app uses AWSSDK.S3 — route S3 traffic through this endpoint instead
# of the NAT to save bytes.
resource "aws_vpc_endpoint" "s3" {
  vpc_id            = aws_vpc.main.id
  service_name      = "com.amazonaws.${var.region}.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = [aws_route_table.private.id]

  tags = { Name = "${local.name}-s3-endpoint" }
}

# ecs cluster, where service will run
resource "aws_ecs_cluster" "pichub_cluster" {
  name = "pichub-ecs-cluster"
}

resource "aws_cloudwatch_log_group" "pichub_service_logs" {
  name = "/ecs/pichub-ecs-service"
}

# ecs task, defining how the container should run
resource "aws_ecs_task_definition" "pichub_task" {
  family                   = "pichub-task"
  network_mode             = "bridge"
  requires_compatibilities = ["EC2"]
  execution_role_arn       = aws_iam_role.ecs_exe_role.arn  # used by ecs
  task_role_arn            = aws_iam_role.ecs_task_role.arn # used by running container 
  cpu                      = "512"
  memory                   = "768"

  container_definitions = jsonencode([{
    name      = "pichub-api-container"
    image     = var.api_image_uri
    essential = true
    cpu       = 512
    memory    = 768

    portMappings = [
      {
        containerPort = 8080,
        hostPort      = 8080,
        protocol      = "tcp"
      },
      {
        containerPort = 8081,
        hostPort      = 8081,
        protocol      = "tcp"
      }
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = "${aws_cloudwatch_log_group.pichub_service_logs.name}"
        "awslogs-region"        = "eu-north-1"
        "awslogs-stream-prefix" = "ecs"
      }
    }
  }])
}

# ecs service, defining how the task def will run on cluster
resource "aws_ecs_service" "pichub_service" {
  name                    = "pichub-ecs-service"
  cluster                 = aws_ecs_cluster.pichub_cluster.id
  task_definition         = aws_ecs_task_definition.pichub_task.arn
  desired_count           = 1
  launch_type             = "EC2"
  enable_ecs_managed_tags = false
}


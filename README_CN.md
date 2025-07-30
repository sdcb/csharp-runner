# 🚀 C\# Runner

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Docker-Host](https://img.shields.io/docker/v/sdcb/csharp-runner-host?sort=semver&logo=docker&label=host)](https://hub.docker.com/r/sdcb/csharp-runner-host)
[![Docker-Worker](https://img.shields.io/docker/v/sdcb/csharp-runner-worker?sort=semver&logo=docker&label=worker)](https://hub.docker.com/r/sdcb/csharp-runner-worker)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

[English](./README.md) | **中文**

C\# Runner 是一个高性能、安全的在线 C\# 代码执行平台。它基于 Host-Worker 架构，通过 Docker 容器隔离执行用户代码，并同时支持 HTTP 和 MCP (Model Context Protocol) 两种协议。

## ✨ 核心特性

  - **🔒 安全可靠**

      - **容器隔离**: 非受信代码在独立的 Docker Worker 容器中执行，确保主机安全。
      - **资源限制**: 支持 CPU、内存和执行超时限制，防止资源滥用。
      - **网络隔离**: Worker 容器网络访问受限。
      - **自动回收**: 可配置 Worker 执行固定次数后自动销毁并重建，保持环境纯净。

  - **⚡ 高性能**

      - **Worker 预热**: 启动时预编译代码，确保首次执行即享最佳性能。
      - **负载均衡**: 采用轮询 (Round-Robin) 算法将任务分发至多个 Worker。
      - **连接池**: 复用 HttpClient，减少网络开销。

  - **🌐 功能丰富**

      - **双协议支持**: 同时提供 HTTP REST API 和 MCP 协议接口。
      - **流式输出**: 基于 Server-Sent Events (SSE) 实时返回代码输出、结果和错误。
      - **易用的 Web UI**: 内置美观的代码编辑器，支持语法高亮和 `Ctrl+Enter` 快捷执行。
      - **开箱即用**: 提供完整的 Docker Compose 解决方案，一键部署。

## 🚀 快速入门

**前置要求:**

  * Docker 和 Docker Compose

**使用 Docker Compose 一键部署:**

```bash
# 下载 docker-compose.yml 文件
curl -L https://raw.githubusercontent.com/sdcb/csharp-runner/refs/heads/master/docker-compose.yml -o docker-compose.yml

# 启动服务 (后台运行)
docker compose up -d
```

部署成功后，在浏览器中打开 `http://localhost:5050` 即可访问 Web 操作界面。

## 🔧 配置

### Docker Compose

`docker-compose.yml` 文件中包含 Host 和 Worker 两个服务。你可以通过修改 `deploy.replicas` 来调整 Worker 的数量，并通过 `environment` 来配置 Worker 的行为。

```yml
services:
  host:
    image: sdcb/csharp-runner-host:latest
    container_name: csharp-runner-host
    ports:
      - "5050:8080"
    restart: unless-stopped

  worker:
    image: sdcb/csharp-runner-worker:latest
    environment:
      - MaxRuns=2           # Worker 最大执行次数 (0=无限制)
      - Register=true       # 是否自动注册到 Host（独立部署时不需要）
      - RegisterHostUrl=http://host:8080
      - WarmUp=false        # 默认由host预热，独立部署时建议打开
    restart: unless-stopped
    depends_on:
      - host
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: 0.50
          memory: 256M
          pids: 32
        reservations:
          cpus: 0.25
          memory: 128M
```

### Worker 环境变量

| 参数              | 说明                               | 默认值          |
| ----------------- | ---------------------------------- | --------------- |
| `MaxRuns`         | Worker 最大执行次数 (0 表示无限制) | `0`             |
| `Register`        | 是否自动注册到 Host 服务           | `false`         |
| `RegisterHostUrl` | Host 服务的注册地址                | `http://host`   |
| `ExposedUrl`      | Worker 对外暴露的 URL (可选)       | `null`          |
| `WarmUp`          | Worker 启动时是否执行预热          | `false`         |
| `MaxTimeout`      | 最大执行超时时间 (毫秒)            | `30000`         |

## 📡 API 使用

### HTTP API (SSE)

通过向 `/api/run` 发送 POST 请求来执行代码，响应将以 Server-Sent Events 流的形式返回。

**请求**

```http
POST /api/run
Content-Type: application/json

{
  "code": "Console.WriteLine(\"Hello, World!\"); return 42;",
  "timeout": 30000
}
```

**响应流**

```http
data: {"kind":"stdout","stdOutput":"Hello, World!"}

data: {"kind":"result","result":42}

data: {"kind":"end","elapsed":150,"stdOutput":"Hello, World!","stdError":""}
```

### MCP 协议

MCP 端点为 `/mcp`，支持 `run_code` 工具调用。

**请求示例**

```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "run_code",
    "arguments": {
      "code": "Console.WriteLine(\"Hello from MCP!\");"
    }
  },
  "id": 1
}
```

## 🏗️ 架构与开发指南

### 系统架构

  - **Host**: 对外提供 API 和 Web 界面，接收代码执行请求，管理 Worker 池并进行负载均衡。
  - **Worker**: 运行在隔离的沙箱环境中，负责实际执行 C\# 代码并返回结果。

### 项目结构

```
src/
├── Sdcb.CSharpRunner.Host/     # Host 服务 (ASP.NET Core)
├── Sdcb.CSharpRunner.Worker/   # Worker 服务 (ASP.NET Core)
└── Sdcb.CSharpRunner.Shared/   # 共享类库 (数据模型等)
```

### 本地开发

1.  **启动 Host 服务**

    ```bash
    cd src/Sdcb.CSharpRunner.Host
    dotnet run
    ```

2.  **启动 Worker 服务**

    ```bash
    cd src/Sdcb.CSharpRunner.Worker
    dotnet run
    ```

### 构建自定义镜像

```bash
# 构建 Host 镜像
dotnet publish ./src/Sdcb.CSharpRunner.Host/Sdcb.CSharpRunner.Host.csproj -c Release /t:PublishContainer /p:ContainerRepository=csharp-runner-host

# 构建 Worker 镜像
dotnet publish ./src/Sdcb.CSharpRunner.Worker/Sdcb.CSharpRunner.Worker.csproj -c Release /t:PublishContainer /p:ContainerRepository=csharp-runner-worker
```

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！请遵循以下步骤：

1.  Fork 本项目
2.  创建您的特性分支 (`git checkout -b feature/AmazingFeature`)
3.  提交您的更改 (`git commit -m 'Add some AmazingFeature'`)
4.  将分支推送到您的 Fork (`git push origin feature/AmazingFeature`)
5.  创建一个 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 详细信息请参阅 [LICENSE](https://www.google.com/search?q=LICENSE) 文件。

-----

⭐ 如果这个项目对你有帮助，请给它一个 Star！
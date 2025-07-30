# 🚀 C\# Runner

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Docker-Host](https://img.shields.io/docker/v/sdcb/csharp-runner-host?sort=semver&logo=docker&label=host)](https://hub.docker.com/r/sdcb/csharp-runner-host)
[![Docker-Worker](https://img.shields.io/docker/v/sdcb/csharp-runner-worker?sort=semver&logo=docker&label=worker)](https://hub.docker.com/r/sdcb/csharp-runner-worker)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**English** | [中文](./README_CN.md)

C\# Runner is a high-performance and secure platform for executing C\# code online. It's built on a Host-Worker architecture that isolates code execution in Docker containers and supports dual protocols: HTTP and MCP (Model Context Protocol).

## ✨ Core Features

  - **🔒 Secure & Reliable**

      - **Container Isolation**: Untrusted code runs in isolated Docker Worker containers, ensuring host safety.
      - **Resource Limits**: Supports CPU, memory, and execution timeout limits to prevent resource abuse.
      - **Network Isolation**: Worker containers have restricted network access.

    <!-- end list -->

      * **Worker Recycling**: Automatically recycles a Worker instance after a configured number of runs to maintain a clean environment.

  - **⚡ High-Performance**

      - **Worker Warm-up**: Pre-compiles code on startup to ensure optimal performance from the very first run.
      - **Load Balancing**: Distributes tasks among multiple Workers using a Round-Robin algorithm.
      - **Connection Pooling**: Reuses HttpClient instances to reduce network overhead.

  - **🌐 Rich Functionality**

      - **Dual Protocol Support**: Offers both an HTTP REST API and an MCP interface.
      - **Streaming Output**: Streams code output, results, and errors in real-time using Server-Sent Events (SSE).
      - **User-friendly Web UI**: Features a clean code editor with syntax highlighting and `Ctrl+Enter` shortcut for execution.
      - **Out-of-the-Box**: Provides a complete Docker Compose solution for one-click deployment.

## 🚀 Quick Start

**Prerequisites:**

  * Docker and Docker Compose

**One-Click Deploy with Docker Compose:**

```bash
# Download the docker-compose.yml file
curl -L https://raw.githubusercontent.com/sdcb/csharp-runner/refs/heads/master/docker-compose.yml -o docker-compose.yml

# Start the services in detached mode
docker compose up -d
```

Once deployed, open your browser to `http://localhost:5050` to access the web UI.

## 🔧 Configuration

### Docker Compose

The `docker-compose.yml` file defines the Host and Worker services. You can scale the number of Workers by changing `deploy.replicas` and configure Worker behavior via `environment` variables.

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
      - MaxRuns=2           # Max runs per worker (0=unlimited)
      - Register=true       # Auto-register to the Host
      - RegisterHostUrl=http://host:8080
      - WarmUp=false        # Enable warm-up (recommended for standalone deployment)
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

### Worker Environment Variables

| Parameter         | Description                                                        | Default         |
| ----------------- | ------------------------------------------------------------------ | --------------- |
| `MaxRuns`         | The maximum number of times a Worker can execute code (0=unlimited). | `0`             |
| `Register`        | Whether to auto-register to the Host service.                      | `false`         |
| `RegisterHostUrl` | The registration URL of the Host service.                          | `http://host`   |
| `ExposedUrl`      | The externally exposed URL of the Worker (optional).               | `null`          |
| `WarmUp`          | Whether to perform a warm-up on startup.                           | `false`         |
| `MaxTimeout`      | Maximum execution timeout in milliseconds.                         | `30000`         |

## 📡 API Usage

### HTTP API (SSE)

Execute code by sending a POST request to `/api/run`. The response is streamed as Server-Sent Events (SSE).

**Request**

```http
POST /api/run
Content-Type: application/json

{
  "code": "Console.WriteLine(\"Hello, World!\"); return 42;",
  "timeout": 30000
}
```

**Response Stream**

```http
data: {"kind":"stdout","stdOutput":"Hello, World!"}

data: {"kind":"result","result":42}

data: {"kind":"end","elapsed":150,"stdOutput":"Hello, World!","stdError":""}
```

### MCP Protocol

The MCP endpoint is at `/mcp` and supports the `run_code` tool.

**Request Example**

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

## 🏗️ Architecture & Development Guide

### System Architecture

  - **Host**: Provides the public API and web UI. It receives requests, manages a pool of Workers, and handles load balancing.
  - **Worker**: Runs in an isolated sandbox environment, responsible for executing the C\# code and returning the result.

### Project Structure

```
src/
├── Sdcb.CSharpRunner.Host/     # Host Service (ASP.NET Core)
├── Sdcb.CSharpRunner.Worker/   # Worker Service (Console App)
└── Sdcb.CSharpRunner.Shared/   # Shared Library (DTOs, etc.)
```

### Local Development

1.  **Run the Host Service**

    ```bash
    cd src/Sdcb.CSharpRunner.Host
    dotnet run
    ```

2.  **Run the Worker Service**

    ```bash
    cd src/Sdcb.CSharpRunner.Worker
    dotnet run
    ```

### Building Custom Images

```bash
# Build the Host image
dotnet publish ./src/Sdcb.CSharpRunner.Host/Sdcb.CSharpRunner.Host.csproj -c Release /t:PublishContainer /p:ContainerRepository=csharp-runner-host

# Build the Worker image
dotnet publish ./src/Sdcb.CSharpRunner.Worker/Sdcb.CSharpRunner.Worker.csproj -c Release /t:PublishContainer /p:ContainerRepository=csharp-runner-worker
```

## 🤝 Contributing

Contributions via Issues and Pull Requests are welcome\! Please follow these steps:

1.  Fork the repository
2.  Create your feature branch (`git checkout -b feature/AmazingFeature`)
3.  Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4.  Push to the branch (`git push origin feature/AmazingFeature`)
5.  Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](https://www.google.com/search?q=LICENSE) file for details.

-----

⭐ If you find this project helpful, please give it a star\!
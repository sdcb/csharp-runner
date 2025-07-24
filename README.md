# 🚀 C# Runner

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Docker-Host](https://img.shields.io/docker/v/sdcb/csharp-runner-host?sort=semver&logo=docker)](https://hub.docker.com/r/sdcb/csharp-runner-host)
[![Docker-Worker](https://img.shields.io/docker/v/sdcb/csharp-runner-worker?sort=semver&logo=docker)](https://hub.docker.com/r/sdcb/csharp-runner-worker)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

C# Runner 是一个高性能、安全的在线 C# 代码执行平台，支持 HTTP 和 MCP（Model Context Protocol）双协议接口。基于 Docker Compose 架构，通过 Host-Worker 分离设计确保代码执行的安全性和性能。

## ✨ 特性

- 🔒 **安全隔离** - 未受信任的 C# 代码在独立的 Worker 容器中执行
- ⚡ **高性能** - Worker 预热机制，首次执行即可获得最佳性能
- 🌐 **双协议支持** - 同时支持 HTTP REST API 和 MCP 协议
- 📊 **实时流式输出** - 基于 Server-Sent Events (SSE) 的实时代码执行反馈
- 🎯 **负载均衡** - 采用 Round-Robin 算法在多个 Worker 之间分发任务
- 🐳 **容器化部署** - 完整的 Docker Compose 解决方案
- 🎨 **Web 界面** - 美观易用的在线代码编辑器和执行环境

## 🏗️ 系统架构

```
├── Host
│   ├── Http
│   │   ├── Api
│   │   └── Pages
│   ├── Mcp
│   ├── Services
│   └── Program.cs
└── Worker
    ├── Handlers
    ├── HostedServices
    ├── HttpClient
    ├── Mcp
    └── Program.cs
```

- **Host**: 处理外部请求，进行负载均衡，管理 Worker 状态。
- **Worker**: 实际执行 C# 代码的沙箱，返回执行结果。

## 🚀 快速开始

### 前置要求

- Docker 和 Docker Compose
- .NET 9 SDK (开发环境)

### 使用 Docker Compose 部署

```bash
# 直接下载docker-compose
curl -L https://raw.githubusercontent.com/sdcb/csharp-runner/refs/heads/master/docker-compose.yml -o docker-compose.yml
# 启动服务
docker compose up -d
```

然后就可以打开浏览器访问 [http://localhost:5050](http://localhost:5050)

### 开发环境运行

1. 启动 Host 服务
```bash
cd src/Sdcb.CSharpRunner.Host
dotnet run
```

2. 启动 Worker 服务
```bash
cd src/Sdcb.CSharpRunner.Worker
dotnet run
```

## 🔧 配置说明

### Docker Compose 配置

```yml
services:
  host:
    image: sdcb/csharp-runner-host:latest
    container_name: csharp-runner-host
    ports:
      - "5050:8080"  # Web UI 和 API 端口
    restart: unless-stopped

  worker:
    image: sdcb/csharp-runner-worker:latest
    environment:
      - MaxRuns=1              # 最大运行次数 (0=无限制)
      - Register=true          # 自动注册到 Host
      - RegisterHostUrl=http://host:8080  # Host 服务地址
    restart: unless-stopped
    depends_on:
      - host
    deploy:
      replicas: 5              # Worker 副本数量
```

### Worker 配置参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `MaxRuns` | Worker 最大执行次数，0 表示无限制 | `0` |
| `Register` | 是否自动注册到 Host 服务 | `false` |
| `RegisterHostUrl` | Host 服务的注册地址 | `http://host` |
| `ExposedUrl` | Worker 对外暴露的 URL (可选) | `null` |
| `WarmUp` | Worker启动时是否执行预热 | `false` |
| `MaxTimeout` | 最大执行超时时间 (毫秒) | `30000` |

## 📡 API 使用

### HTTP API

#### 执行 C# 代码

```http
POST /api/run
{
  "code": "Console.WriteLine(\"Hello, World!\"); return 42;",
  "timeout": 30000
}
```

**响应** (Server-Sent Events)
```http
data: {"kind":"stdout","stdOutput":"Hello, World!"}

data: {"kind":"result","result":42}

data: {"kind":"end","elapsed":150,"stdOutput":"Hello, World!","stdError":""}
```

### MCP 协议

MCP 端点：`/mcp`

支持的工具：
- `run_code` - 在沙箱环境中执行 C# 代码

#### 示例请求
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

## 🎨 Web 界面特性

- 🖥️ **代码编辑器** - 支持语法高亮、Tab 缩进
- ⚡ **快捷执行** - Ctrl+Enter 快速运行代码
- 📊 **实时输出** - 显示标准输出、错误输出和执行结果
- ⏱️ **超时设置** - 可配置代码执行超时时间
- 🎯 **状态显示** - 实时显示 Worker 数量和执行状态

## 🔒 安全特性

- **容器隔离** - 每个 Worker 运行在独立的 Docker 容器中
- **资源限制** - 支持 CPU 和内存使用限制
- **执行超时** - 防止无限循环和长时间运行
- **网络隔离** - Worker 容器具有受限的网络访问权限
- **运行次数限制** - 可配置 Worker 的最大执行次数

## 🧩 支持的 C# 功能

内置引用的程序集和命名空间：
```csharp
// 支持的命名空间
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.Security.Cryptography;
using System.Numerics;
// ... 更多
```

## 📊 性能优化

1. **Worker 预热** - 启动时预先编译和执行示例代码
2. **连接池** - 复用 HttpClient 连接
3. **Round-Robin 调度** - 均匀分发请求到多个 Worker
4. **SSE 流式传输** - 实时传输执行结果，提升用户体验

## 🛠️ 开发指南

### 项目结构

```
src/
├── Sdcb.CSharpRunner.Host/     # Host 服务
│   ├── Controllers/            # API 控制器
│   ├── Mcp/                   # MCP 协议实现
│   ├── Pages/                 # Razor Pages
│   └── Program.cs
├── Sdcb.CSharpRunner.Worker/   # Worker 服务
│   ├── Handlers.cs            # 代码执行处理器
│   └── Program.cs
└── Sdcb.CSharpRunner.Shared/   # 共享类库
    └── Models/                # 数据传输对象
```

### 构建镜像
# 构建 Host 镜像
```bash
dotnet publish ./src/Sdcb.CSharpRunner.Host/Sdcb.CSharpRunner.Host.csproj -c Release /t:PublishContainer /p:ContainerRepository=csharp-runner-host
```

# 构建 Worker 镜像
```bash
dotnet publish ./src/Sdcb.CSharpRunner.Worker/Sdcb.CSharpRunner.Worker.csproj -c Release /t:PublishContainer /p:ContainerRepository=csharp-runner-worker
```

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🙋‍♂️ 支持

- 🐛 Bug 报告：[GitHub Issues](https://github.com/sdcb/csharp-runner/issues)

---

⭐ 如果这个项目对你有帮助，请给它一个 Star！
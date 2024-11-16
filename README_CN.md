## [English](README.md) || 简体中文

# VRC面部追踪- Pico 4 Pro 模块

> 让VRCFT接收来自串流助手、PICO互联或企业串流的面部追踪数据

## 为 **VRCFaceTracking** 安装 **Pico 4 Pro 模块**。

1. 下载并安装最新版的串流助手、PICO互联（PICO 4 Pro）或企业串流（PICO 4 Enterprise）
2. 下载并安装 Pico 4 Pro 模块.
  * 自动安装
    * 通过 VRCFaceTracking 的 **官方模块库** 页面安装 **Pico 4 Pro 模块** 。
  * 手动安装
    * 下载[最新版本](https://github.com/regzo2/PicoStreamingAssistantFTUDP/releases)
    * 将 **.dll** 后缀的文件放入 `%appdata%/VRCFaceTracking/CustomLibs`。

3. 启动 VRCFaceTracking ！
> [!IMPORTANT]
> 要使用 Pico 4 Pro 模块，您需要确保您的 VRCFaceTracking 版本在 5.0.0.0 以上

有关更多信息，请参阅 [PICO module VRCFT wiki entry （英文）](https://docs.vrcft.io/docs/hardware/pico4pe).

## 常见问题

>Pico 4 Pro 没有通过模块连接至 VRCFT

确保您的Pico 4设备能够使用面部跟踪。
目前，只有Pico 4 Pro/Enterprise能够与此模块一起使用。
确保您拥有能够发送面部跟踪数据的最新流媒体软件。
  
## 协议 / 分发

**(待定)**.

## 编译这个模块

### 在 Docker 容器中编译

> [!NOTE]
> 受网络因素影响，不推荐使用此方式。

您需要[Docker](https://www.docker.com/)以便运行以下步骤。

- 运行`bash-ci/build.sh`

您可以在`artifacts\Pico4SAFTXextTrackingModule.dll`中找到该模块

### 使用 Visual Studio 2022 编译

- 安装 [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) 并选择 .Net 桌面开发 负载
- 使用 `git clone https://github.com/regzo2/PicoStreamingAssistantFTUDP.git --recurse-submodules` 命令克隆本仓库
- 使用 Visual Studio 2022 打开位于 `PicoStreamingAssistantFTUDP` 文件夹下的解决方案 `PicoStreamingAssistantFTUDP.sln`
- 编译此解决方案会自动编译`VRCFaceTracking.Core`和模块

您会在这找到编译完成的模块： `PicoStreamingAssistantFTUDP\PicoStreamingAssistantFTUDP\bin\x64\Debug\net7.0\Pico4SAFTExtTrackingModule.dll`.

### 使用 Dotnet SDK 编译
- 安装 [Dotnet 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0)
- 使用 `git clone https://github.com/regzo2/PicoStreamingAssistantFTUDP.git --recurse-submodules` 命令克隆本仓库
- 运行命令 `dotnet build PicoStreamingAssistantFTUDP`
- 此命令会自动编译`VRCFaceTracking.Core`和模块

您会在这找到编译完成的模块： `PicoStreamingAssistantFTUDP\PicoStreamingAssistantFTUDP\bin\x64\Debug\net7.0\Pico4SAFTExtTrackingModule.dll`.

## 运行测试

### 在 Docker 容器中运行测试

> [!NOTE]
> 受网络因素影响，不推荐使用此方式。

- 运行`bash-ci/tests.sh`

您可以在 `PicoStreamingAssistantFTUDP\PicoStreamingAxistantFTTests\TestResults\dotnet-test-results.xml` 中找到摘要

### 使用 Visual Studio 2022 运行测试

> [!NOTE]
> 请确保您有安装 .Net 7 SDK，否则测试可能无法正常工作

- 在 Visual Studio 的顶部菜单栏上，单击“测试”，然后单击“运行所有测试”。

### 使用 Dotnet SDK 测试

> [!NOTE]
> 请确保您有安装 .Net 7 SDK，否则测试可能无法正常工作

- 运行命令 `dotnet test PicoStreamingAssistantFTUDP`

## 贡献
- [Ben](https://github.com/benaclejames/) 的 VRCFaceTracking!
- [TofuLemon](https://github.com/ULemon/) 帮助测试、故障排除和提供关键信息，从而开发该模块！
- [rogermiranda1000](https://github.com/rogermiranda1000) 更新到最新版的协议
- [lonelyicer](https://github.com/lonelyicer) 最近的贡献。

## [English](https://github.com/regzo2/PicoStreamingAssistantFTUDP/blob/vrcfacetracking-module/README.md) || 简体中文

# VRC面部追踪- Pico 4 Pro 模块

> 让VRCFT接收来自PICO互联的面部追踪数据

## 为 **VRCFaceTracking** 安装 **Pico 4 Pro 模块**。

1. 安装最新版的PICO互联
2. 下载并安装 Pico 4 Pro 模块.
* [最新版本](https://github.com/regzo2/PicoStreamingAssistantFTUDP/releases)
  * 安装器
    * 通过 VRCFaceTracking 的 **官方模块库** 页面安装 **Pico 4 Pro 模块** 。
  * 手动
    * 将 **.dll** 后缀的文件放入 `%appdata%/VRCFaceTracking/CustomLibs`。

**要使用 Pico 4 Pro 模块，你需要确保你的 VRCFaceTracking 版本在 5.0.0.0 以上**

3. 启动 VRCFaceTracking!

## 常见问题

>Pico 4 Pro 没有通过模块连接至 VRCFT

请确保你的PICO互联为最新版本，并且确保 `%appdata%/PICO Connect/settings.json` 文件内 `faceTrackingTransferProtocol` 对应的值为 `2`

  
## 协议 / 分发

**(TBD)**.

## 编译这个模块
- 使用 [Visual Studio 2022](https://visualstudio.microsoft.com/es/vs/)
- Clone [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking) 存储库到本项目的文件夹内
- 编译 VRCFaceTracking.Core

你会在这找到编译完成的模块： `PicoStreamingAssistantFTUDP\PicoStreamingAssistantFTUDP\bin\Debug\net7.0\Pico4SAFTExtTrackingModule.dll`.

#### 编译 VRCFaceTracking.Core

- 如果你遇到有关 `vcruntime140.dll` 的错误, 修改 ( `VRCFaceTracking\VRCFaceTracking.Core\VRCFaceTracking.Core.csproj`) 的引用为 `C:\Windows\System32\vcruntime140.dll`
- 如果你遇到有关 `fti_osc.dll` 的错误,修改 ( `VRCFaceTracking\VRCFaceTracking.Core\VRCFaceTracking.Core.csproj`) 的引用为 ? (我刚刚把他删除了w)

## 贡献
- [Ben](https://github.com/benaclejames/) 的 VRCFaceTracking!
- [TofuLemon](https://github.com/ULemon/) 帮助测试、故障排除和提供关键信息，从而开发该模块！
- [rogermiranda1000](https://github.com/rogermiranda1000) 更新到最新版的协议
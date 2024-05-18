## English || [简体中文](README_CN.md)

# VRCFaceTracking - Pico 4 Pro/Enterprise Module

> Let VRCFaceTracking use the Pico 4 Pro/Enterprise's face tracking data via the Streaming Assistant, PICO Connect, or Business Streaming.

## Setup **Pico 4 Pro Module** for **VRCFaceTracking**.

1. Download the latest Streaming Assistant/PICO Connect (for PICO 4 Pro), or Business Conect (for PICO 4 Enterprise)
2. Download and install the Pico 4 Pro Module

* Installer
  * Install `Pico4SAFTExtTrackingModule` from VRCFaceTracking's **Module Registry** tab.
* Manual
  * Download the [Latest Release](https://github.com/regzo2/PicoStreamingAssistantFTUDP/releases)
  * Include the supplied **.dll** release in `%appdata%/VRCFaceTracking/CustomLibs` (create the folder if is not there).

3. Launch VRCFaceTracking. **VRCFaceTracking v5.0.0.0 is required to use Pico 4 Pro Module.**


For more information, refer to the [PICO module VRCFT wiki entry](https://docs.vrcft.io/docs/hardware/pico4pe).

## Troubleshooting

> The Pico 4 Pro is not connecting to VRCFaceTracking using the module.

Make sure that your Pico 4 device is capable of using face tracking. 
At this time only the Pico 4 Pro/Enterprise is capable of being used with this module.
Make sure you have the latest streaming software capable of sending 
face tracking data.

  
## Licenses / Distribution

**(TBD)**.

## Compiling this module

### Docker (recommended)

You'll need [Docker](https://www.docker.com/) in order to run the following steps.

- Run `bash ci/build.sh`

### Manual
- Use [Visual Studio 2022](https://visualstudio.microsoft.com/es/vs/)
- Clone [VRCFaceTracking 5.2.3.0](https://github.com/benaclejames/VRCFaceTracking/tree/5.2.3.0); this is done automatically if you run `git submodule update --init --recursive`
- Compile VRCFaceTracking.Core
- Finally, compile the PICO Module

You'll find the module in `PicoStreamingAssistantFTUDP\PicoStreamingAssistantFTUDP\bin\Debug\net7.0\Pico4SAFTExtTrackingModule.dll`.

## Running the tests

### Docker

- Run `bash ci/tests.sh`

### Manual

- On Visual Studio, on the top bar, go to "Test", then "Run all tests".

## Credits
- [Ben](https://github.com/benaclejames/) for VRCFaceTracking!
- [TofuLemon](https://github.com/ULemon/) with help testing, troubleshooting and providing crucial information that lead to the development of this module!
- [rogermiranda1000](https://github.com/rogermiranda1000) for updating to the latest SA protocol.
- [lonelyicer](https://github.com/lonelyicer) for the recent contributions.
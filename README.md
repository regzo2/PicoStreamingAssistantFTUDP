# VRCFaceTracking - Pico 4 Pro Module

> Let VRCFaceTracking use the Pico 4 Pro's face tracking data via the Pico Streaming Assistant.

## Setup **Pico 4 Pro Module** for **VRCFaceTracking**.

1. (TBP) Download the latest Streaming Assistant capable of OSC tracking *current*
2. Remove or rename `pico_et_ft_bt_bridge.exe` in `%YourStreamingAssistantDirectory%\driver\bin\win64\`
2. Download and install the Pico 4 Pro Module.
* [Latest Release](https://github.com/regzo2/PicoStreamingAssistantFTUDP/releases)
  * Installer
    * (TBD) Simply install **Pico 4 Pro Module** from VRCFaceTracking's **Module Registry** tab.
  * Manual
    * Include the supplied **.dll** release in `%appdata%/VRCFaceTracking/CustomLibs`. 
**VRCFaceTracking is required to use Pico 4 Pro Module.**

3. Launch VRCFaceTracking!

## Troubleshooting

> The Pico 4 Pro is not connecting to VRCFaceTracking using the module.

Make sure that your Pico 4 device is capable of using face tracking. 
At this time only the Pico 4 Pro is capable of being used with this module.
Make sure you have the (unreleased) latest Streaming Assistant capable of sending 
face tracking data.

  
## Licenses / Distribution

**(TBD)**.

## Credits
- [Ben](https://github.com/benaclejames/) for VRCFaceTracking!
- [TofuLemon](https://github.com/ULemon/) with help testing, troubleshooting and providing crucial information that lead to the development of this module!

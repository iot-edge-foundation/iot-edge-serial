# Azure IoT Edge Serial Module

## Azure IoT Edge Serial Port Communication module for Linux & Windows

Using this module, developers can build Azure IoT Edge solutions with Serial (RS232) Port (the module is currently not available in Windows environment, please use Linux host + Linux container to play with the module) connectivity. The Serial module is an Azure IoT Edge module, capable of reading data from serial port devices and publishing data to the Azure IoT Hub via the Edge framework. Developers can modify the module tailoring to any scenario.

TODO: insert image

There are prebuilt Serial module container images ready at here TODO: insert link to CR for you to quickstart the experience of Azure IoT Edge on your target device or simulated device.

Visit http://azure.com/iotdev to learn more about developing applications for Azure IoT.

## Azure IoT Edge Compatibility

Current version of the module is targeted for the Azure IoT Edge GA.

Find more information about Azure IoT Edge at here TODO: insert link to Edge docs.

## Target Device Setup

### Platform Compatibility

Azure IoT Edge is designed to be used with a broad range of operating system platforms. Modbus module has been tested on the following platforms:

- ~~Windows 10 Enterprise (version 1709) x64~~
- ~~Windows 10 IoT Core (version 1709) x64~~
- Linux x64
- ~~Linux arm32v7~~

### Device Setup

- Windows 10 Desktop
- Windows 10 IoT Core
- Linux

TODO: insert links

## Build Environment Setup

Serial module is a .NET Core 2.1 application, which is developed and built based on the guidelines in Azure IoT Edge document. Please follow this link to setup the build environment.

Basic requirement:

- Docker CE
- .NET Core 2.1 SDK


# DynamicEmission-XR

This is a proof of concept project for using XR (Extended Reality) to visualize dynamic emissions data in a 3D environment.

## Installation and Setup

1. Download and install Unity Hub from [Unity Hub](https://unity.com/download).
2. Sign up for a Unity account if you don't have one.
3. When asked to install the newest version of Unity, click "Skip Installation".
4. Clone this repository to your local machine.
5. Open Unity Hub and click on the "Add" button to add an existing project.
6. You will be prompted to install the required Unity version (6000.2.4f1). Click "Install" and follow the instructions in Unity.
7. When asked to install modules, select Android Build Support.

## Setup of Meta Quest 3 Headset

1. This project is designed to work with the Meta Quest 3 headset. This is because of its advanced passthrough capabilities, which allow for a more immersive XR experience.
2. Follow the official Meta Quest 3 setup guide to set up your headset: [Meta Quest 3 Setup Guide](https://developers.meta.com/horizon/documentation/unity/unity-env-device-setup/#headset-setup).

## Build application

1. In Unity, go to `File > Build Settings`.
2. Select `Android` as the target platform and click `Switch Platform`.
3. Select `XR Plug-in Management` from the Project Settings and enable `Oculus`.
4. Connect your Meta Quest 3 headset to your computer via USB.
5. In the Build Settings window, click `Build and Run` to deploy the application to your headset.

The application should now launch on your Meta Quest 3 headset, allowing you to visualize dynamic emissions data in an immersive XR environment.

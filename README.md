# PollutionView-MR

This is a proof of concept project for using MR (Mixed Reality) to visualize dynamic emissions data in a 3D environment.

## Installation

1. Download and install Unity Hub from [https://unity.com/download](https://unity.com/download).
2. Sign up for a Unity account if you don't have one.
3. When asked to install the newest version of Unity, click "Skip Installation".
4. Clone this repository to your local machine.
5. Open the project folder in Unity Hub (Add > select folder).
6. You will be prompted to install the required Unity version (6000.0.24f1). Click "Install" and follow the instructions.
7. When selecting modules during installation, ensure the following are checked:
   - Android Build Support
   - Android SDK & NDK Tools
   - OpenJDK
8. Once installation completes, Unity Hub will automatically open the project.
9. Unity will automatically download and install all required packages (this may take several minutes on first launch).

## Setup

### Meta Quest 3 Configuration

1. This project is designed to work with the Meta Quest 3 headset due to its advanced passthrough capabilities, which allow for a more immersive XR experience.
2. Follow the official Meta Quest 3 setup guide to configure your headset: [Meta Quest 3 Setup Guide](https://developers.meta.com/horizon/documentation/unity/unity-env-device-setup/#headset-setup).
3. Enable Developer Mode on your Meta Quest 3 headset (required for deploying custom applications).

### Data Preparation

1. Place your input data file in `Assets/StreamingAssets/`.
2. In Unity, navigate to `Tools > Data Asset Baker > Bake the dataset`.
3. Wait for the baking process to complete.
4. Locate the baked dataset in the Assets folder.
5. In the Hierarchy, find the `Baker` GameObject.
6. Drag the baked dataset into the `Dataset Asset` field on the Baker component.

### Building the Application

1. In Unity, go to `File > Build Settings`.
2. Ensure `Android` is selected as the target platform. If not, select it and click `Switch Platform` (this may take a few minutes).
3. Open `Edit > Project Settings > XR Plug-in Management` and ensure `Oculus` is enabled under the Android tab.
4. Connect your Meta Quest 3 headset to your computer via USB-C cable.
5. Ensure your headset is turned on and you've allowed USB debugging when prompted on the headset.
6. In the Build Profiles tab (within Build Settings), change the `Run Device` from "Default" to your connected Meta Quest 3 headset.
7. Click `Build and Run` to compile and deploy the application to your headset.

The application should now launch on your Meta Quest 3 headset, allowing you to visualize your emissions data in an immersive XR environment.

### Troubleshooting

- If the headset isn't detected in the Run Device dropdown, check that USB debugging is enabled and try a different USB cable.
- If the build fails, ensure all required packages have finished installing (check the Package Manager).
- Make sure Developer Mode is enabled on your Meta Quest 3 device.

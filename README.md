# Oculus WebView Example

This Unity project demonstrates how to view and interact with web content in VR on Oculus Quest and Rift using [Vuplex 3D WebView](https://developer.vuplex.com/webview/overview). It includes the [Oculus Integration](https://assetstore.unity.com/packages/tools/integration/oculus-integration-82022) package, so the only thing you must import is [3D WebView for Android with Gecko Engine](https://store.vuplex.com/webview/android-gecko) or [3D WebView for Windows](https://store.vuplex.com/webview/windows-mac).

<p align="center">
  <img alt="demo" src="./demo.gif" width="640">
</p>

## Steps taken to create this project

1. Created a new project with Unity 2020.3.23.
2. Installed the [Oculus Integration package](https://assetstore.unity.com/packages/tools/integration/oculus-integration-82022) (v35).
3. Installed [3D WebView](https://developer.vuplex.com) ([.gitignore](https://github.com/vuplex/oculus-webview-example/blob/69d404181ba188937c124d154d7b1eab6173f609/.gitignore#L62)).
4. Created a new scene named OculusWebViewDemoScene to combine 3D WebView's [WebViewPrefab](https://developer.vuplex.com/webview/WebViewPrefab) and [Keyboard](https://developer.vuplex.com/webview/Keyboard) prefabs with the Oculus OVRCameraRig.
5. Added support for controllers by adding the Oculus [UIHelpers prefab](Assets/Oculus/SampleFramework/Core/DebugUI/Prefabs/UIHelpers.prefab) to the scene.
6. Made the following tweaks to the scene's UIHelpers prefab instance:
    - Updated the OVRInputModule script's "Ray Transform" and "Joypad Click Button" values
    - Disabled the Sphere cursor visual
    - Enabled the LaserPointer's line renderer and set its material

## License

The Oculus Integration library located in Assets/Oculus is Copyright © Facebook Technologies, LLC and its affiliates. All rights reserved. The Oculus Integration Library is licensed under the [Oculus SDK License](https://developer.oculus.com/licenses/sdk-3.5/).

All other code and assets are Copyright © Vuplex, Inc and licensed under the MIT License.

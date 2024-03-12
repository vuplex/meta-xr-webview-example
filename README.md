# Meta Quest WebView Example

This Unity project demonstrates how to view and interact with web content in VR on Meta Quest headsets (2, 3, Pro) using [Vuplex 3D WebView](https://developer.vuplex.com/webview/overview). It includes the [Meta XR All-In-One SDK](https://assetstore.unity.com/packages/tools/integration/meta-xr-all-in-one-sdk-269657), so the only additional thing you must import is [3D WebView for Android](https://store.vuplex.com/webview/android).

Note: 3D WebView's native Android plugins can't run in the editor, so a [mock webview](https://support.vuplex.com/articles/mock-webview) implementation is used by default while running in the editor unless [3D WebView for Windows and macOS](https://store.vuplex.com/webview/windows-mac) is also installed.

<p align="center">
  <img alt="demo" src="./demo.gif" width="640">
</p>

## Steps taken to create this project

1. Created a new project with Unity 2022.3.17 using the default 3D project template.
2. Imported the [Meta XR All-In-One SDK](https://assetstore.unity.com/packages/tools/integration/meta-xr-all-in-one-sdk-269657) (v62.0).
3. Added the [Meta XR Interaction SDK OVR Samples](https://developer.oculus.com/downloads/package/meta-xr-interaction-sdk-ovr-samples/) package and imported its "Example Scenes" samples.
4. Made a copy of the RayExamples scene from the OVR Samples.
5. Imported [3D WebView](https://developer.vuplex.com).
6. Made the following modifications to the RayExamples scene copy:
    - Renamed the scene MetaXRWebViewExample.
    - Added a [CanvasWebViewPrefab](https://developer.vuplex.com/webview/CanvasWebViewPrefab) and [CanvasKeyboard](https://developer.vuplex.com/webview/CanvasKeyboard) to the middle Canvas.
    - Removed unneeded objects from the scene.
    - Added the MetaXRWebViewExample.cs script to demonstrate using 3D WebView's scripting APIs.

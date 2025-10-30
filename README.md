# Meta Quest WebView Example

This Unity project demonstrates how to view and interact with web content in VR on Meta Quest headsets (2, 3, 3S, Pro) using [Vuplex 3D WebView](https://developer.vuplex.com/webview/overview). It includes the [Meta XR All-In-One SDK](https://assetstore.unity.com/packages/tools/integration/meta-xr-all-in-one-sdk-269657), so the only additional thing you must import is [3D WebView for Android](https://store.vuplex.com/webview/android).

Note: 3D WebView's native Android plugins can't run in the editor, so a [mock webview](https://support.vuplex.com/articles/mock-webview) implementation is used by default while running in the editor unless [3D WebView for Windows and macOS](https://store.vuplex.com/webview/windows-mac) is also installed.

<p align="center">
  <img alt="demo" src="./demo.gif" width="640">
</p>

## Steps taken to create this project

1. Created a new project with Unity 6.2 (6000.2.10f1) using the default Universal 3D project template.
2. Opened the Unity Package Manager and did the following:
    - Imported v78.0 of the [Meta XR All-In-One SDK](https://assetstore.unity.com/packages/tools/integration/meta-xr-all-in-one-sdk-269657).
    - On the page for the Meta XR Interaction SDK package (com.meta.xr.sdk.interaction.ovr), clicked on the "Samples" tab and clicked "Import" button for "Example Scenes".
4. Imported [3D WebView](https://developer.vuplex.com).
5. Copied the [RayExamples](Assets%2FSamples%2FMeta%20XR%20Interaction%20%E2%80%8BSDK%2F78.0.0%2FExample%20Scenes%2FRayExamples.unity) sample scene to Assets/Scenes.
6. Made the following modifications to the RayExamples scene copy:
    - Renamed the scene to MetaXRWebViewExample.
    - Added a [CanvasWebViewPrefab](https://developer.vuplex.com/webview/CanvasWebViewPrefab) and [CanvasKeyboard](https://developer.vuplex.com/webview/CanvasKeyboard) to its curved Canvas.
    - Removed unneeded objects from the scene.
    - Added the MetaXRWebViewExample.cs script to demonstrate using 3D WebView's scripting APIs.

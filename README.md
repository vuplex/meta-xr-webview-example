# Oculus WebView Example

This Unity project demonstrates how to use the [Vuplex 3D WebView asset](https://developer.vuplex.com/webview/overview) with Oculus Quest, Oculus Go, and Gear VR. It includes the [Oculus Integration](https://assetstore.unity.com/packages/tools/integration/oculus-integration-82022) package, so the only thing you must import is the [3D WebView for Android plugin](https://assetstore.unity.com/packages/tools/gui/3d-webview-for-android-137030).

<p align="center">
  <img alt="demo" src="./demo.gif" width="480">
</p>

## Steps taken to create this project

1. Created a new Unity project
2. Installed v1.37 of the Oculus Integration package from the Asset Store
3. Imported the [Vuplex 3D WebView asset](https://developer.vuplex.com/webview/overview) ([.gitignore](https://github.com/vuplex/oculus-webview-example/blob/69d404181ba188937c124d154d7b1eab6173f609/.gitignore#L62))
4. Made a few updates to OVRInputModule.cs (based on [this blog post](https://developer.oculus.com/blog/adding-gear-vr-controller-support-to-the-unity-vr-samples/)) to add support for controllers ([f2531f1](https://github.com/vuplex/oculus-webview-example/commit/f2531f1d0325e16a2b1e6d510b9661ab48fc2f0a#diff-93e3a5aae2911095791d5ebd91bbb75dR27))
5. Added an OculusWebViewDemoScene to combine the [WebViewPrefab](https://developer.vuplex.com/webview/WebViewPrefab) and [Keyboard](https://developer.vuplex.com/webview/Keyboard) from the Vuplex 3D WebView asset with the Oculus camera rig and input system ([3b04776](https://github.com/vuplex/oculus-webview-example/commit/3b04776a265b7431518892af779af92aff5cdd43#diff-149997e6ff04f2f79b8a812b1ba2fb9aR1))

## License

The Oculus Integration library located in Assets/Oculus is Copyright © Facebook Technologies, LLC and its affiliates. All rights reserved. The Oculus Integration Library is licensed under the [Oculus SDK License](https://developer.oculus.com/licenses/sdk-3.5/).

All other code and assets are Copyright © Vuplex, Inc and licensed under the MIT License.

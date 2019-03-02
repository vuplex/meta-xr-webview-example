using UnityEngine;
using Vuplex.WebView;
using UnityEngine.XR;

class OculusWebViewDemo : MonoBehaviour {

    WebViewPrefab _webViewPrefab;
    Keyboard _keyboard;

    void Start() {

        #if UNITY_ANDROID && !UNITY_EDITOR
            // Use the alternative input event system for Oculus Go:
            // https://developer.vuplex.com/webview/AndroidWebView#GloballyUseAlternativeInputEventSystem
            AndroidWebView.GloballyUseAlternativeInputEventSystem(true);
        #endif

        // Create a 0.6 x 0.4 instance of the prefab.
        _webViewPrefab = WebViewPrefab.Instantiate(0.6f, 0.4f);
        _webViewPrefab.transform.parent = transform;
        _webViewPrefab.transform.localPosition = new Vector3(0, 0f, 0.6f);
        _webViewPrefab.transform.LookAt(transform);
        _webViewPrefab.Initialized += (sender, e) => {
            _webViewPrefab.WebView.LoadUrl("https://www.google.com");
        };

        // Add the keyboard under the main webview.
        _keyboard = Keyboard.Instantiate();
        _keyboard.transform.parent = _webViewPrefab.transform;
        _keyboard.transform.localPosition = new Vector3(0, -0.41f, 0);
        _keyboard.transform.localEulerAngles = new Vector3(0, 0, 0);
        // Hook up the keyboard so that characters are routed to the main webview.
        _keyboard.InputReceived += (sender, e) => _webViewPrefab.WebView.HandleKeyboardInput(e.Value);
    }

    void Update() {

        transform.position = Camera.main.transform.position + new Vector3(0, 0.2f, 0);
    }
}

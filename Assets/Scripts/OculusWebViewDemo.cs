using UnityEngine;
using Vuplex.WebView;
using UnityEngine.XR;

class OculusWebViewDemo : MonoBehaviour {

    WebViewPrefab _webViewPrefab;
    Keyboard _keyboard;

    void Start() {

        // Create a 0.6 x 0.3 instance of the prefab.
        _webViewPrefab = WebViewPrefab.Instantiate(0.6f, 0.3f);
        _webViewPrefab.transform.parent = transform;
        _webViewPrefab.transform.localPosition = new Vector3(0, 0f, 0.4f);
        _webViewPrefab.transform.LookAt(transform);
        _webViewPrefab.Initialized += (sender, e) => {
            // Use the alternative input event system for Oculus Go:
            // https://developer.vuplex.com/webview/AndroidWebView#UseAlternativeInputEventSystem
            // In the future, this will be a static so that it doesn't need to be
            // called for each webview.
            ((AndroidWebView) _webViewPrefab.WebView).UseAlternativeInputEventSystem(true);
            _webViewPrefab.WebView.LoadUrl("https://www.google.com");
        };

        // Add the keyboard under the main webview.
        _keyboard = Keyboard.Instantiate();
        _keyboard.WebViewPrefab.Initialized += (sender, e) => ((AndroidWebView) _keyboard.WebViewPrefab.WebView).UseAlternativeInputEventSystem(true);
        _keyboard.transform.parent = _webViewPrefab.transform;
        _keyboard.transform.localPosition = new Vector3(0, -0.31f, 0);
        _keyboard.transform.localEulerAngles = new Vector3(0, 0, 0);
        // Hook up the keyboard so that characters are routed to the main webview.
        _keyboard.InputReceived += (sender, e) => _webViewPrefab.WebView.HandleKeyboardInput(e.Value);
    }

    void Update() {

        transform.position = Camera.main.transform.position + new Vector3(0, 0.2f, 0);
    }
}

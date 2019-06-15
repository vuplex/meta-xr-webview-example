/************************************************************************************

Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.  

See SampleFramework license.txt for license terms.  Unless required by applicable law 
or agreed to in writing, the sample code is provided “AS IS” WITHOUT WARRANTIES OR 
CONDITIONS OF ANY KIND, either express or implied.  See the license for specific 
language governing permissions and limitations under the license.

************************************************************************************/

using UnityEngine;
using System;
using System.IO;

public class MoviePlayerSample : MonoBehaviour
{
    private bool    videoPausedBeforeAppPause = false;

	private UnityEngine.Video.VideoPlayer videoPlayer = null;
	private OVROverlay          overlay = null;
	private Renderer 			mediaRenderer = null;

    public bool isPlaying { get; private set; }

    private RenderTexture copyTexture;
    private Material externalTex2DMaterial;

    public string MovieName;

    /// <summary>
    /// Initialization of the movie surface
    /// </summary>
    void Awake()
    {
        Debug.Log("MovieSample Awake");

        mediaRenderer = GetComponent<Renderer>();

        videoPlayer = GetComponent<UnityEngine.Video.VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();

        overlay = GetComponent<OVROverlay>();
        if (overlay == null)
            overlay = gameObject.AddComponent<OVROverlay>();

        // set shape to Equirect
        overlay.currentOverlayShape = OVROverlay.OverlayShape.Equirect;

        // set source and dest matrices for 180 video
        overlay.overrideTextureRectMatrix = true;
        overlay.SetSrcDestRects(new Rect(0, 0, 0.5f, 1.0f), new Rect(0.5f, 0, 0.5f, 1.0f), new Rect(0.25f, 0, 0.5f, 1.0f), new Rect(0.25f, 0, 0.5f, 1.0f));

        // disable it to reset it.
        overlay.enabled = false;
        // only can use external surface with native plugin
        overlay.isExternalSurface = NativeVideoPlayer.IsAvailable;
        // only mobile has Equirect shape
        overlay.enabled = Application.platform == RuntimePlatform.Android;

#if UNITY_EDITOR
        overlay.currentOverlayShape = OVROverlay.OverlayShape.Quad;
        overlay.enabled = true;
#endif
    }

    private System.Collections.IEnumerator Start()
    {
        if (mediaRenderer.material == null)
		{
			Debug.LogError("No material for movie surface");
            yield break;
		}

        // wait 1 second to start (there is a bug in Unity where starting
        // the video too soon will cause it to fail to load)
        yield return new WaitForSeconds(1.0f);

        if (!string.IsNullOrEmpty(MovieName))
        {
#if UNITY_EDITOR
            // in editor, just pull in the movie file from wherever it lives (to test without putting in streaming assets)
            var guids = UnityEditor.AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(MovieName));

            if (guids.Length > 0)
            {
                string video = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                Play(video);
            }
#else
            Play(Application.streamingAssetsPath +"/" + MovieName);
#endif
        }
    }

    public void Play(string moviePath)
    {
        if (moviePath != string.Empty)
        {
            Debug.Log("Playing Video: " + moviePath);
            if (overlay.isExternalSurface)
            {
                OVROverlay.ExternalSurfaceObjectCreated surfaceCreatedCallback = () =>
                {
                    Debug.Log("Playing ExoPlayer with SurfaceObject");
                    NativeVideoPlayer.PlayVideo(moviePath, overlay.externalSurfaceObject);
                };

                if (overlay.externalSurfaceObject == IntPtr.Zero)
                {
                    overlay.externalSurfaceObjectCreated = surfaceCreatedCallback;
                }
                else
                {
                    surfaceCreatedCallback.Invoke();
                }
            }
            else
            {
                Debug.Log("Playing Unity VideoPlayer");
                videoPlayer.url = moviePath;
                videoPlayer.Prepare();
                videoPlayer.Play();                
            }

            Debug.Log("MovieSample Start");
            isPlaying = true;
        }
        else
        {
            Debug.LogError("No media file name provided");
        }
    }

    public void Play()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.Play();
        }
        else
        {
            videoPlayer.Play();
        }
        isPlaying = true;
    }

    public void Pause()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.Pause();
        }
        else
        {
            videoPlayer.Pause();
        }
        isPlaying = false;
    }

	void Update()
	{
        if (!overlay.isExternalSurface)            
        {
            var displayTexture = videoPlayer.texture != null ? videoPlayer.texture : Texture2D.blackTexture;
            if (overlay.enabled)
            {
                if (overlay.textures[0] != displayTexture)
                {
                    // OVROverlay won't check if the texture changed, so disable to clear old texture
                    overlay.enabled = false;
                    overlay.textures[0] = displayTexture;
                    overlay.enabled = true;
                }
            }
            else
            {
                mediaRenderer.material.mainTexture = displayTexture;
                mediaRenderer.material.SetVector("_SrcRectLeft", overlay.srcRectLeft.ToVector());
                mediaRenderer.material.SetVector("_SrcRectRight", overlay.srcRectRight.ToVector());
            }
        }
	}

    public void Rewind()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.SetPlaybackSpeed(-1);
        }
        else
        {
            videoPlayer.playbackSpeed = -1;
        }
    }
    
    public void Stop()
    {
        if (overlay.isExternalSurface)
        {
            NativeVideoPlayer.Stop();
        }
        else
        {
            videoPlayer.Stop();
        }

        isPlaying = false;
    }

    /// <summary>
    /// Pauses video playback when the app loses or gains focus
    /// </summary>
    void OnApplicationPause(bool appWasPaused)
    {
        Debug.Log("OnApplicationPause: " + appWasPaused);
        if (appWasPaused)
        {
            videoPausedBeforeAppPause = !isPlaying;
        }
        
        // Pause/unpause the video only if it had been playing prior to app pause
        if (!videoPausedBeforeAppPause)
        {
            if (appWasPaused)
            {
                Pause();
            }
            else
            {
                Play();
            }
        }
    }
}

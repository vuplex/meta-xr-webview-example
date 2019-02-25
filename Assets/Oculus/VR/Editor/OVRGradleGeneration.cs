/************************************************************************************

Copyright   :   Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.

Licensed under the Oculus SDK License Version 3.4.1 (the "License");
you may not use the Oculus SDK except in compliance with the License,
which is provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

https://developer.oculus.com/licenses/sdk-3.4.1

Unless required by applicable law or agreed to in writing, the Oculus SDK
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

************************************************************************************/

using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
using UnityEngine;

#if UNITY_2018_1_OR_NEWER && UNITY_ANDROID

public class OVRGradleGeneration : IPostGenerateGradleAndroidProject
{
	public int callbackOrder { get { return 3; } }

	public void OnPostGenerateGradleAndroidProject(string path)
	{
		Debug.Log("OVRGradleGeneration triggered.");
		Debug.LogFormat("  GearVR or Go = {0}  Quest = {1}", OVRDeviceSelector.isTargetDeviceGearVrOrGo, OVRDeviceSelector.isTargetDeviceQuest);

		bool isQuestOnly = OVRDeviceSelector.isTargetDeviceQuest && !OVRDeviceSelector.isTargetDeviceGearVrOrGo;

		if (isQuestOnly)
		{
			if (File.Exists(Path.Combine(path, "build.gradle")))
			{
				try
				{
					string gradle = File.ReadAllText(Path.Combine(path, "build.gradle"));

					int v2Signingindex = gradle.IndexOf("v2SigningEnabled false");
					if (v2Signingindex != -1)
					{
						gradle = gradle.Replace("v2SigningEnabled false", "v2SigningEnabled true");
						System.IO.File.WriteAllText(Path.Combine(path, "build.gradle"), gradle);
					}
				}
				catch (System.Exception e)
				{
					Debug.LogWarningFormat("Unable to overwrite build.gradle, error {0}", e.Message);
				}
			}
			else
			{
				Debug.LogWarning("Unable to locate build.gradle");
			}
		}
	}
}

#endif

// MIT License

// Copyright (c) 2020 Fernando Ramallo

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

// 
//// SoundRecorder.cs
// Tool to record sound clips from the microphone in the Unity Editor
//
//	Usage (eg. inside the OnGUI function of Editor scripts):
//  
// 	if (GUI.Button) {
//     if (!SoundRecorder.IsRecording()) {
//       SetPlayhead(0);
//       SetPlayback(true);
//       SoundRecorder.StartRecording(totalFrameLength / (float)m_FramesPerSecond);
//     } else {
//       SoundRecorder.StopRecording();
//     }
//   } 
// 

[System.Serializable]
internal class SoundRecorder {

	internal class Sound {
		private AudioClip m_Clip;
		private AudioClip m_ClipReversed;
		internal string m_SavedGUID { get; private set; }

		private bool m_Playing = false;

		internal bool HasRecording() { return m_Clip != null && m_Clip.length > 0; }
		internal AudioClip Clip { get { return m_Clip; } }
		
		internal void SetClip(AudioClip Clip) {
			m_Clip = Clip;
			m_RecordingClip.name = "Recording";
			// Workaround for loud peak at the start
			float[] data = new float[m_Clip.samples * m_Clip.channels];
			m_Clip.LoadAudioData();
			m_Clip.GetData(data, 0);
			for(int i = 0; i < 100; i++) {
				//Debug.Log(data[i]);
			}
			// make reversed version
			m_ClipReversed = AudioClip.Create(m_Clip.name + "_reversed", m_Clip.samples, 
				m_Clip.channels, m_Clip.frequency, false);
			if (m_Clip.GetData(data, 0)) {
				System.Array.Reverse(data);
				m_ClipReversed.SetData(data, 0);
			} else {
				Debug.LogError("Couldn't get data");
			}
		}

		internal void PlayPreview(float startTime = 0, bool reversed = false) {
			if (!HasRecording() || SoundRecorder.IsRecording()) {
				return;
			}
			AudioClip clip = reversed ? m_ClipReversed : m_Clip;
			// Methods from https://github.com/MattRix/UnityDecompiled/blob/cc432a3de42b53920d5d5dae85968ff993f4ec0e/UnityEditor/UnityEditor/AudioUtil.cs
			if (m_Playing) {
				StopPreview();
			}
			m_Playing = true;
			int startSample = Mathf.FloorToInt(startTime * clip.samples);
			//Debug.Log("Play at " + startTime + " sample " + startSample);
			if (reversed) startSample = Mathf.Abs(startSample - clip.samples);
			EditorUtils.InvokeMethod(typeof(AudioImporter), "UnityEditor.AudioUtil", "PlayClip",
				new System.Type[]{typeof(AudioClip), typeof(int)}, new object[]{clip, startSample});
		}
		internal void StopPreview() {
			if (!HasRecording()) {
				return;
			}
			EditorUtils.InvokeMethod(typeof(AudioImporter), "UnityEditor.AudioUtil", "StopClip", typeof(AudioClip), m_Clip);
			EditorUtils.InvokeMethod(typeof(AudioImporter), "UnityEditor.AudioUtil", "StopClip", typeof(AudioClip), m_ClipReversed);
			m_Playing = false;
		}

		internal void Save(string assetPath, bool bTrim = true) {
			if (!HasRecording()) {
				// No recording
				return;
			}
			if (bTrim) {
				var oldClip = m_Clip;
				SetClip(SavWav.TrimSilence(m_Clip, 0.01f));
				Resources.UnloadAsset(oldClip);
			}
			SavWav.Save(Path.Combine(Application.dataPath.TrimEnd("Assets".ToCharArray()), assetPath), m_RecordingClip);
			AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
			m_SavedGUID = AssetDatabase.AssetPathToGUID(assetPath);
		}
		
	}

	static private List<Sound> m_Sounds;
	static private int m_CurrentSound = 0;

	static private AudioClip m_RecordingClip;
	
	static internal bool IsRecording() { return Microphone.IsRecording(null); }
	static internal bool HasRecordings() { 
		return Sounds != null && Sounds.Count > 0 && Sounds[0].HasRecording(); 
	}

	static bool HasMicrophones() { return Microphone.devices.Length > 0; }

	static internal Sound CurrentSound { get { return m_Sounds[m_CurrentSound]; } }
	static internal List<Sound> Sounds { get { return m_Sounds; } }

	internal static void Reset() {
		m_RecordingClip = null;
		m_Sounds = new List<Sound>() { new Sound() };
		m_CurrentSound = 0;
		StopPreview();
	}

	internal static void StartRecording(float length) {
		if (!HasMicrophones()) {
			return;
		}
		if (CurrentSound == null) {
			m_Sounds.Add(new Sound());
			m_CurrentSound = m_Sounds.Count - 1;
		}
		StopPreview();
		m_RecordingClip = Microphone.Start(null, false, Mathf.CeilToInt(length), 44100);
		m_RecordingClip.hideFlags = HideFlags.HideAndDontSave;
	}

	internal static void StopRecording() {
		if (!HasMicrophones()) {
			return;
		}
		Microphone.End(null);
		CurrentSound.SetClip(m_RecordingClip);
	}

	internal static void PlayPreview(float startTime = 0, bool reversed = false) {
		if (CurrentSound == null) {
			return;
		}
		CurrentSound.PlayPreview(startTime, reversed);
	}
	internal static void StopPreview() {
		if (CurrentSound == null) {
			return;
		}
		CurrentSound.StopPreview();
	}

	internal static void Save(string sContainerPath, string sAnimationName, bool bTrim = true) {
		for(int i = 0; i < m_Sounds.Count; i++) {
			if (!m_Sounds[i].HasRecording())
				continue;
			string assetPath = Path.Combine(
				sContainerPath, 
				string.Format("{0}_{1}.wav", sAnimationName, i + 1)
			);
			m_Sounds[i].Save(assetPath, bTrim);
		}
		AssetDatabase.Refresh();
	}
}

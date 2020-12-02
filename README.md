# Unity Editor Microphone Recorder

This is a snippet for recording microphone audio in the Unity Editor, extracted from Doodle Studio 95!

No warranties, not sure it still works!


## Usage

Usage (eg. inside the OnGUI function of Editor scripts):

```
if (GUI.Button) {
	if (!SoundRecorder.IsRecording()) {
		SetPlayhead(0);
		SetPlayback(true);
		SoundRecorder.StartRecording(totalFrameLength / (float)m_FramesPerSecond);
	} else {
		SoundRecorder.StopRecording();
	}
} 
```

using SDD.Events;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class VoiceRecorder : MonoBehaviour
{
    [Tooltip("Path to the file used to record or replay the recorded data.")]
    public string fileName = "SpeechRecording.txt";

    [Tooltip("Whether to start playing the recorded data, right after the scene start.")]
    public bool playAtStart = false;

    // singleton instance of the class
    private static VoiceRecorder instance = null;

    // whether it is recording or playing saved data at the moment
    private bool isRecording = false;

    private VoiceRecognizer voiceRecognizer = null;

    // time variables used for recording and playing
    private ulong liRelTime = 0;
    private float fStartTime = 0f;
    private float fCurrentTime = 0f;
    private int fCurrentFrame = 0;

    // player variables
    private StreamReader fileReader = null;
    private float fPlayTime = 0f;
    private string sPlayLine = string.Empty;

    private string filePath;

    public static VoiceRecorder Instance
    {
        get
        {
            return instance;
        }
    }


    // starts recording
    public bool StartRecording()
    {
        if (isRecording)
            return false;

        isRecording = true;

        // stop recording if there is no file name specified
        if (fileName.Length == 0)
        {
            isRecording = false;

            Debug.LogError("No file to save.");
        }

        if (isRecording)
        {
            Debug.Log("Recording started.");

            // delete the old csv file
            if (fileName.Length > 0 && File.Exists(filePath + fileName))
            {
                File.Delete(filePath + fileName);
            }

            // initialize times
            fStartTime = fCurrentTime = Time.time;
            fCurrentFrame = 0;
        }

        return isRecording;
    }

    // stops recording or playing
    public void StopRecordingOrPlaying()
    {
        if (isRecording)
        {
            isRecording = false;

            string sSavedTimeAndFrames = string.Format("{0:F3}s., {1} frames.", (fCurrentTime - fStartTime), fCurrentFrame);
            Debug.Log("Recording stopped @ " + sSavedTimeAndFrames);
        }

        //if (infoText != null)
        //{
        //    infoText.text = "Say: 'Record' to start the recorder, or 'Play' to start the player.";
        //}
    }

    // returns if file recording is in progress at the moment
    public bool IsRecording()
    {
        return isRecording;
    }

    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<SetPathEvents>(SetPathEventsHandler);
    }

    private void SetPathEventsHandler(SetPathEvents e)
    {
        this.filePath = e.Path;
    }

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<SetPathEvents>(SetPathEventsHandler);
    }


    // ----- end of public functions -----


    void Awake()
    {
        filePath = "";
        SubscribeEvents();
        instance = this;
    }

    void Start()
    {
        //if (infoText != null)
        //{
        //    infoText.text = "Say: 'Record' to start the recorder, or 'Play' to start the player.";
        //}

        if (!voiceRecognizer)
        {
            voiceRecognizer = VoiceRecognizer.Instance;
        }
        else
        {
            Debug.Log("KinectManager not found, probably not initialized.");
        }
    }

    void Update()
    {
        if (isRecording)
        {
            // save the body frame, if any
            if (voiceRecognizer == null)
            {
                return;
            }

            fCurrentTime = Time.time;
            string sBodyFrame = voiceRecognizer.GetTextRecognized();

            System.Globalization.CultureInfo invCulture = System.Globalization.CultureInfo.InvariantCulture;

            if (sBodyFrame.Length > 0)
            {
#if !UNITY_WSA
                using (StreamWriter writer = File.AppendText(filePath + fileName))
                {
                    string sRelTime = string.Format(invCulture, "{0:F3}", (fCurrentTime - fStartTime));
                    writer.WriteLine(sRelTime + "|" + sBodyFrame);

                    fCurrentFrame++;
                }
#else
					string sRelTime = string.Format(invCulture, "{0:F3}", (fCurrentTime - fStartTime));
					Debug.Log(sRelTime + "|" + sBodyFrame);
#endif
            }
        }
    }

    void OnDestroy()
    {
        // don't forget to release the resources
        CloseFile();
        isRecording = false;
        CancelEvents();
    }

    // close the file and disable the play mode
    private void CloseFile()
    {
        // close the file
        if (fileReader != null)
        {
            fileReader.Dispose();
            fileReader = null;
        }
    }

}

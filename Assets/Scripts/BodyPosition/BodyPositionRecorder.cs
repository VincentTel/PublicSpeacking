using SDD.Events;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BodyPositionRecorder : MonoBehaviour
{
    [Tooltip("Path to the file used to record or replay the recorded data.")]
    public string fileName = "BodyPositionRecording.txt";

    [Tooltip("Whether to start playing the recorded data, right after the scene start.")]
    public bool playAtStart = false;

    // singleton instance of the class
    private static BodyPositionRecorder instance = null;

    // whether it is recording or playing saved data at the moment
    private bool isRecording = false;
    private bool isPlaying = false;

    private AvatarPosition avatarPosition = null;

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

    public static BodyPositionRecorder Instance
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

        // avoid recording an playing at the same time
        if (isPlaying && isRecording)
        {
            CloseFile();
            isPlaying = false;

            Debug.Log("Playing stopped.");
        }

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


    // starts playing
    public bool StartPlaying()
    {
        if (isPlaying)
            return false;

        isPlaying = true;

        // avoid recording an playing at the same time
        if (isRecording && isPlaying)
        {
            isRecording = false;
            Debug.Log("Recording stopped.");
        }

        // stop playing if there is no file name specified
        if (fileName.Length == 0 || !File.Exists(filePath + fileName))
        {
            isPlaying = false;
            Debug.LogError("File not found: " + filePath + fileName);
        }

        if (isPlaying)
        {
            Debug.Log("Playing started.");

            // initialize times
            fStartTime = fCurrentTime = Time.time;
            fCurrentFrame = -1;

            // open the file and read a line
#if !UNITY_WSA
            fileReader = new StreamReader(filePath + fileName);
#endif
            ReadLineFromFile();
        }

        return isPlaying;
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

        if (isPlaying)
        {
            // close the file, if it is playing
            CloseFile();
            isPlaying = false;

            Debug.Log("Playing stopped.");
        }
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
        filePath = e.Path;
    }

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<SetPathEvents>(SetPathEventsHandler);
    }

    // returns if file-play is in progress at the moment
    public bool IsPlaying()
    {
        return isPlaying;
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
        if (!avatarPosition)
        {
            avatarPosition = AvatarPosition.Instance;
        }

        if (playAtStart)
        {
            StartPlaying();
        }
    }

    void Update()
    {
        if (isRecording)
        {
            if (avatarPosition == null)
            {
                return; //No need to go to the next if since isRecording and isPlaying can't be true in the same time
            }

            fCurrentTime = Time.time;
            Vector3 bodyPosition = avatarPosition.GetBodyPosition();
            string sBodyFrame = bodyPosition.x + ";" + bodyPosition.z;

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

        if (isPlaying)
        {
            // wait for the right time
            fCurrentTime = Time.time;
            float fRelTime = fCurrentTime - fStartTime;

            if (sPlayLine != null && fRelTime >= fPlayTime)
            {
                // then play the line
                if (avatarPosition && sPlayLine.Length > 0)
                {
                    avatarPosition.SetBodyData(sPlayLine);
                }

                // and read the next line
                ReadLineFromFile();
            }

            if (sPlayLine == null)
            {
                // finish playing, if we reached the EOF
                StopRecordingOrPlaying();
            }
        }
    }

    void OnDestroy()
    {
        // don't forget to release the resources
        CloseFile();
        isRecording = isPlaying = false;
        CancelEvents();
    }

    // reads a line from the file
    private bool ReadLineFromFile()
    {
        if (fileReader == null)
            return false;

        // read a line
        sPlayLine = fileReader.ReadLine();
        if (sPlayLine == null)
            return false;

        System.Globalization.CultureInfo invCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.NumberStyles numFloat = System.Globalization.NumberStyles.Float;

        // extract the unity time and the body frame
        char[] delimiters = { '|' };
        string[] sLineParts = sPlayLine.Split(delimiters);

        if (sLineParts.Length >= 2)
        {
            float.TryParse(sLineParts[0], numFloat, invCulture, out fPlayTime);
            sPlayLine = sLineParts[1];
            fCurrentFrame++;

            return true;
        }

        return false;
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
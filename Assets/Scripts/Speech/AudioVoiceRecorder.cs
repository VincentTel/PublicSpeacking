using SDD.Events;
using System;
using System.Collections;
using UnityEngine;

public class AudioVoiceRecorder : MonoBehaviour
{
    [Tooltip("Path to the file used to record or replay the recorded data.")]
    public string fileName = "AudioVoice";

    [Tooltip("Whether to start playing the recorded data, right after the scene start.")]
    public bool playAtStart = false;

    [Tooltip("Sprite transforms that will be used to display the countdown, when recording starts.")]
    public Transform[] countdown;

    [Header("Audio Settings")]
    public int audioFrequency = 44100;
    public int audioLength = 1200; // Default 20 min
    public AudioSource audioSource = null;

    private WWW www;
    // recording parameters
    private bool isRecording = false;
    private bool isPlaying = false;
    private bool isCountingDown = false;

    private string filePath;

    //Audioclip to record in
    private AudioClip myClip;

    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.AddListener<PresentationFinishEvent>(PresentationFinishEventHandler);
        EventManager.Instance.AddListener<ViewModeStartEvent>(ViewModeStartEventHandler);
        EventManager.Instance.AddListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
        EventManager.Instance.AddListener<SetPathEvents>(SetPathEventsHandler);
        EventManager.Instance.AddListener<EndViewModeEvent>(EndViewModelEventHandler);
    }

    private void EndViewModelEventHandler(EndViewModeEvent e)
    {
        StartOrStopPlayer();
    }

    private void SetPathEventsHandler(SetPathEvents e)
    {
        this.filePath = e.Path;
    }

    void EndRecording(int position)
    {
        //Capture the current clip data
        var soundData = new float[myClip.samples * myClip.channels];
        myClip.GetData(soundData, 0);

        //Create shortened array for the data that was used for recording
        var newData = new float[position * myClip.channels];

        //Microphone.End (null);
        //Copy the used samples to a new array
        for (int i = 0; i < newData.Length; i++)
        {
            newData[i] = soundData[i];
        }

        //One does not simply shorten an AudioClip,
        //    so we make a new one with the appropriate length
        var newClip = AudioClip.Create(myClip.name, position, myClip.channels, myClip.frequency, false);
        newClip.SetData(newData, 0);        //Give it the data from the old clip

        myClip = newClip;
    }

    private void StartOrStopRecording()
    {
        if (!isPlaying && !isCountingDown)
        {
            isCountingDown = true;
            StartCoroutine(CountdownAndStartRecording());
        }
    }

    private void StartOrStopPlayer()
    {
        if (!isCountingDown)
        {
            StartOrStopPlaying();
        }
    }

    private void PresentationStartEventHandler(PresentationStartEvent e)
    {
        StartOrStopRecording();
    }

    private void PresentationFinishEventHandler(PresentationFinishEvent e)
    {
        StartOrStopRecording();
    }

    private void ViewModeStartEventHandler(ViewModeStartEvent e)
    {
        StartOrStopPlayer();
    }

    private void ViewModeFinishEventHandler(ViewModeFinishEvent e)
    {
        StartOrStopPlayer();
    }

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.RemoveListener<PresentationFinishEvent>(PresentationFinishEventHandler);
        EventManager.Instance.RemoveListener<ViewModeStartEvent>(ViewModeStartEventHandler);
        EventManager.Instance.RemoveListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
        EventManager.Instance.RemoveListener<SetPathEvents>(SetPathEventsHandler);
        EventManager.Instance.RemoveListener<EndViewModeEvent>(EndViewModelEventHandler);
    }

    void Awake()
    {
        filePath = "";
        SubscribeEvents();
    }

    void OnDestroy()
    {
        CancelEvents();
    }

    // counts down (from 3 for instance), then starts recording
    private IEnumerator CountdownAndStartRecording()
    {
        // count down
        if (!isRecording && countdown != null && countdown.Length > 0)
        {
            Debug.LogError("Boucle countdown");

            for (int i = 0; i < countdown.Length; i++)
            {
                if (countdown[i])
                    countdown[i].gameObject.SetActive(true);

                yield return new WaitForSeconds(1f);

                if (countdown[i])
                    countdown[i].gameObject.SetActive(false);
            }
        }

        isCountingDown = false;

        if (!isRecording)
        {
            // start recording
            isRecording = true;
            // Start the mic
            myClip = Microphone.Start(null, false, audioLength, audioFrequency);
        }
        else
        {
            // stop recording
            isRecording = false;

            int position = Microphone.GetPosition(null);
            // stop the mic
            Microphone.End(null);
            EndRecording(position);
            //Save the clip
            SavWav.Save(filePath + fileName, myClip);
        }
    }

    // start or stop re-playing
    private void StartOrStopPlaying()
    {
        if (!isPlaying)
        {
            // start playing
            isPlaying = true;

            //Play the audioClip
            StartCoroutine(StartSong("file://" + filePath + fileName + ".wav"));
        }
        else
        {
            // stop playing
            isPlaying = false;

            //Stop the audioClip
            audioSource.Stop();
        }
    }

    public IEnumerator StartSong(string path)
    {
        www = new WWW(path);
        if (www.error != null)
        {
            Debug.Log(www.error);
        }
        else
        {
            audioSource.clip = www.GetAudioClip();
            while (audioSource.clip.loadState != AudioDataLoadState.Loaded)
                yield return new WaitForSeconds(0.1f);
            audioSource.Play();
        }
    }

}

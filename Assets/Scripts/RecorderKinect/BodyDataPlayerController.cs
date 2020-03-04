using UnityEngine;
using System.Collections;
using com.rfilkov.kinect;
using SDD.Events;
using System;

namespace com.rfilkov.components
{
    /// <summary>
    /// BodyDataPlayerController starts and stops the body data recording and replaying.
    /// </summary>
    public class BodyDataPlayerController : MonoBehaviour
    {
        [Tooltip("Sprite transforms that will be used to display the countdown, when recording starts.")]
        public Transform[] countdown;

        [Tooltip("Sprite transform that will be used to display recording in progress.")]
        public Transform recIcon;

        [Tooltip("Sprite transform that will be used to display re-playing in progress.")]
        public Transform playIcon;


        // reference to BodyDataRecorderPlayer
        private BodyDataRecorderPlayer saverPlayer;
        private KinectManager kinectManager;

        // recording parameters
        private bool isRecording = false;
        private bool isPlaying = false;
        private bool isCountingDown = false;

        #region Custom
        private void SubscribeEvents()
        {
            EventManager.Instance.AddListener<PresentationStartEvent>(PresentationStartEventHandler);
            EventManager.Instance.AddListener<PresentationFinishEvent>(PresentationFinishEventHandler);
            EventManager.Instance.AddListener<ViewModeStartEvent>(ViewModeStartEventHandler);
            EventManager.Instance.AddListener<ViewModeFinishEvent>(ViewModeFinishEventHandler);
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
        }

        void Awake()
        {
            SubscribeEvents();
        }

        void OnDestroy()
        {
            CancelEvents();
        }
        #endregion

        void Start()
        {
            saverPlayer = BodyDataRecorderPlayer.Instance;
            kinectManager = KinectManager.Instance;
        }

        void Update()
        {
            if (!saverPlayer || !kinectManager || !kinectManager.IsInitialized())
                return;

            if (isRecording && !saverPlayer.IsRecording())
            {
                // recording stopped
                isRecording = false;

                if (recIcon)
                {
                    recIcon.gameObject.SetActive(false);
                }
            }

            if (isPlaying && !saverPlayer.IsPlaying())
            {
                // playing stopped
                isPlaying = false;

                if (playIcon)
                {
                    playIcon.gameObject.SetActive(false);
                }
            }

            // Jump-key (Space) - start or stop recording
            //if (Input.GetButtonDown("Jump") && !isPlaying && !isCountingDown)
            //{
            //    isCountingDown = true;
            //    StartCoroutine(CountdownAndStartRecording());
            //}

            // Fire1-key (Ctrl) - start or stop playing
            //if (Input.GetButtonDown("Fire1") && !isRecording && !isCountingDown)
            //{
            //    StartOrStopPlaying();
            //}

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

            if (saverPlayer)
            {
                if (!isRecording)
                {
                    // start recording
                    isRecording = true;

                    if (recIcon)
                    {
                        recIcon.gameObject.SetActive(true);
                    }

                    saverPlayer.StartRecording();
                }
                else
                {
                    // stop recording
                    isRecording = false;

                    if (recIcon)
                    {
                        recIcon.gameObject.SetActive(false);
                    }

                    saverPlayer.StopRecordingOrPlaying();
                }
            }
        }
        
        // start or stop re-playing
        private void StartOrStopPlaying()
        {
            if (saverPlayer)
            {
                if (!isPlaying)
                {
                    // start playing
                    isPlaying = true;

                    if (playIcon)
                    {
                        playIcon.gameObject.SetActive(true);
                    }

                    saverPlayer.StartPlaying();
                }
                else
                {
                    // stop playing
                    isPlaying = false;

                    if (playIcon)
                    {
                        playIcon.gameObject.SetActive(false);
                    }

                    saverPlayer.StopRecordingOrPlaying();
                }
            }
        }
    }
}

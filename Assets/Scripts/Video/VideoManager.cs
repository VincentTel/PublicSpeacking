using System;
using RockVR.Video;
using SDD.Events;
using UnityEngine;

public class VideoManager : MonoBehaviour
{
    private void SubscribeEvents()
    {
        EventManager.Instance.AddListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.AddListener<PresentationFinishEvent>(PresentationFinishEventHandler);
        EventManager.Instance.AddListener<SetPathEvents>(SetPathEventsHandler);
    }

    private void PresentationStartEventHandler(PresentationStartEvent e)
    {
        Debug.Log("Start recording...");
        RockVR.Video.VideoCaptureCtrl.instance.StartCapture();
    }

    private void PresentationFinishEventHandler(PresentationFinishEvent e)
    {
        Debug.Log("Stop recording...");
        RockVR.Video.VideoCaptureCtrl.instance.StopCapture();
    }

    private void CancelEvents()
    {
        EventManager.Instance.RemoveListener<PresentationStartEvent>(PresentationStartEventHandler);
        EventManager.Instance.RemoveListener<PresentationFinishEvent>(PresentationFinishEventHandler);
        EventManager.Instance.RemoveListener<SetPathEvents>(SetPathEventsHandler);
    }

    void Awake()
    {
        SubscribeEvents();
    }

    void OnDestroy()
    {
        CancelEvents();
    }

    private void SetPathEventsHandler(SetPathEvents e)
    {
        VideoCapture[] videoCaptureComponent = FindObjectsOfType<VideoCapture>();

        foreach (VideoCapture component in videoCaptureComponent)
        {
            component.customPathFolder = e.Path;
        }
    }
}

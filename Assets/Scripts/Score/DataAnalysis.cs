using com.rfilkov.kinect;
using SDD.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class DataAnalysis : MonoBehaviour
{
    // TEMPORARY ANALYSIS NEEDS TO BE CHANGED BY THE FINAL SYSTEM

    /**
     * Definition of a test here :
     * a data is good or not.
     * if it is good, we add the score for one parameter completely
     * otherwise, if it is bad, we put 0 as the score for the data
     * 
     * THERE IS NO MIDDLE! It is either good or bad, not so-so.
     * 
     * The addition of all the parameter will determine if the score total is good / ok / bad
     * 
     **/
    public Text feedBackTextBox;

    [Range(0, 180)]
    public float maxFeetAngle = 60;
    public float maxFeetAlignment = .5f;
    public float maxFeetDistance = .5f;
    public float minFeetDistance = .25f;

    private int dataNumber; // The number of different data that we collect in this analysis

    // All data manager
    GazeRayRenderer gazeRayRenderer = null;
    KinectManager kinectManager = null;

    // Data nmber from managers
    //Gaze : test if the user looks the audience
    private int gazerRayRendererDataNumber = 1;
    //Feet & Arms : test if the user is in the direction of the audience
    // if he does not put his arm to the sky
    // and if the feet are not too or not enought spreads
    private int kinectManagerDataNumber = 3;

    // Score linker
    ScoreManager scoreManager = null;

    private string[] methodsNames = { "IsFeetDirectionGood", "IsFeetPositionGood", "IsArmPositionGood", "IsGazeGood" };
    private float scoreByParameter;
    private bool isScoreSystemValid;

    //Feedback strings
    private string feetPositionFeedback = "\nFeet spreading not good";
    private string feetDirectionFeedback = "\nYou must face the audience";
    private string armPositionFeedback = "\nDon't raise your hands";
    private string gazeFeedback = "\nLook at the audience";

    // Start is called before the first frame update
    void Start()
    {
        dataNumber = gazerRayRendererDataNumber + kinectManagerDataNumber;

        gazeRayRenderer = GazeRayRenderer.Instance;
        if (gazeRayRenderer == null)
        {
            Debug.LogError("GazeRayRenderer not linked. It will not be considerer for the score");
            dataNumber -= gazerRayRendererDataNumber;
        }

        kinectManager = KinectManager.Instance;
        if (kinectManager == null)
        {
            Debug.LogError("KinectManager not linked. It will not be considerer for the score");
            dataNumber -= kinectManagerDataNumber;
        }

        // If no data can be used, no need to do anything
        if (dataNumber == 0)
        {
            isScoreSystemValid = false;
            return;
        }

        scoreManager = ScoreManager.Instance;
        if (scoreManager == null)
        {
            Debug.LogError("ScoreManager not linked. The Score will not be used");
            isScoreSystemValid = false;
            return;
        }

        isScoreSystemValid = true;
        scoreByParameter = scoreManager.GetMaxScore() / dataNumber;
    }

    // Update is called once per frame
    void Update()
    {
        // No score manager so don't need to register anything
        if (!isScoreSystemValid)
        {
            return;
        }

        float score = 0;

        RemoveFeedBack();
        // Do all tests (loop on methods name to avoid chained if in writing)
        foreach (string methodName in methodsNames)
        {
            MethodInfo method = this.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            bool result = (bool)method.Invoke(this, null);
            if (result)
            {
                score += scoreByParameter;
            }
        }

        // Set the final Score
        scoreManager.SetScore(score);
    }

    private void AddFeedBack(string feedbackText)
    {
        if (feedBackTextBox.text.Contains(feedbackText))
        {
            return;
        }

        feedBackTextBox.text += feedbackText;
    }

    private void RemoveFeedBack()
    {
        feedBackTextBox.text = "";
    }

    private ulong GetKinectUserId()
    {
        List<int> userIndices = kinectManager.GetAllUserIndices();
        return kinectManager.GetUserIdByIndex(userIndices.Any() ? userIndices.First() : 0);
    }

    private bool IsFeetDirectionGood()
    {
        if (kinectManager == null)
        {
            return false;
        }

        ulong userId = GetKinectUserId();

        Vector3 ankleLeft = kinectManager.GetJointPosition(userId, KinectInterop.JointType.AnkleLeft);
        Vector3 footLeft = kinectManager.GetJointPosition(userId, KinectInterop.JointType.FootLeft);
        Vector3 ankleRight = kinectManager.GetJointPosition(userId, KinectInterop.JointType.AnkleLeft);
        Vector3 footRight = kinectManager.GetJointPosition(userId, KinectInterop.JointType.FootLeft);

        float angleLeft = Vector3.Angle(Vector3.forward, ankleLeft - footLeft);
        float angleRight = Vector3.Angle(Vector3.forward, ankleRight - footRight);

        float angle = Math.Max(angleLeft, angleRight);

        if (angle < maxFeetAngle)
        {
            return true;
        }

        AddFeedBack(feetDirectionFeedback);
        return false;
    }

    private bool IsFeetPositionGood()
    {
        if (kinectManager == null)
        {
            return false;
        }

        ulong userId = GetKinectUserId();

        Vector3 footLeft = kinectManager.GetJointPosition(userId, KinectInterop.JointType.FootLeft);
        Vector3 footRight = kinectManager.GetJointPosition(userId, KinectInterop.JointType.FootRight);

        float absX = Math.Abs(footLeft.x - footRight.x);

        if (Math.Abs(footLeft.z - footRight.z) < maxFeetAlignment &&
            absX > minFeetDistance &&
            absX < maxFeetDistance)
        {
            return true;
        }

        AddFeedBack(feetPositionFeedback);
        return false;
    }

    private bool IsArmPositionGood()
    {
        if (kinectManager == null)
        {
            return false;
        }

        ulong userId = GetKinectUserId();

        float handLeft = kinectManager.GetJointPosition(userId, KinectInterop.JointType.HandLeft).y;
        float shoulderLeft = kinectManager.GetJointPosition(userId, KinectInterop.JointType.ShoulderLeft).y;
        float handRight = kinectManager.GetJointPosition(userId, KinectInterop.JointType.HandRight).y;
        float shoulderRight = kinectManager.GetJointPosition(userId, KinectInterop.JointType.ShoulderRight).y;

        if (handLeft < shoulderLeft || handRight < shoulderRight)
        {
            return true;
        }

        AddFeedBack(armPositionFeedback);
        return false;
    }

    private bool IsGazeGood()
    {
        if (gazeRayRenderer == null)
        {
            return false;
        }

        Vector3 gazeOrigin = gazeRayRenderer.GetRayOrigin();
        Vector3 gazeDirection = gazeRayRenderer.GetRayDirection();

        RaycastHit hit;
        // Does the ray intersect any objects excluding the player layer
        if (Physics.Raycast(gazeOrigin, gazeDirection, out hit))
        {
            if (hit.transform.tag == "Audience")
            {
                return true;
            }
        }

        AddFeedBack(gazeFeedback);
        return false;
    }
}

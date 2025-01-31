﻿using LZ4Sharp;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;


namespace com.rfilkov.kinect
{
    /// <summary>
    /// NetClientInterface is a sensor-interface that receives the sensor data over the network instead from a connected device.
    /// </summary>
    public class NetClientInterface : DepthSensorBase
    {
        [Tooltip("Host name or IP address of the network server.")]
        public string serverHost = "localhost";

        [Tooltip("The base port for all server frame streams (each frame stream listens on separate port).")]
        public int serverBasePort = 11000;

        [Tooltip("Whether to get the body index frames from the server along with the body frames.")]
        public bool getBodyIndexFrames = false;

        [Tooltip("UI-Text to display client status messages.")]
        public UnityEngine.UI.Text clientStatusText;


        // sensor frame servers
        private TcpNetClient controlFrameClient = null;
        private TcpNetClient colorFrameClient = null;
        private TcpNetClient depthFrameClient = null;
        private TcpNetClient infraredFrameClient = null;
        private TcpNetClient bodyDataFrameClient = null;
        private TcpNetClient bodyIndexFrameClient = null;
        private TcpNetClient poseFrameClient = null;
        private TcpNetClient depth2colorFrameClient = null;
        private TcpNetClient color2depthFrameClient = null;
        private TcpNetClient color2bodyIndexFrameClient = null;

        // sensor data
        private KinectInterop.SensorData sensorData = null;

        // control frame
        private KinectInterop.NetSensorData netSensorData = null;
        //private ulong lastControlFrameTime = 0;
        //private bool bSensorDataMsgSent = false;
        private bool bGotNetSensorData = false;
        private bool bSetNetSensorData = false;

        private bool bGotDST = false;
        private bool bGotCST = false;

        // keep-alive
        private ulong lastKeepAliveFrameTime = 0;
        private const int keepAliveInterval = 20000000;  // 2 seconds

        // disconnect-reconnect
        private ulong latestDataReceivedAt = 0;
        private const int disconnectAfter = 300000000;  // 10 seconds

        private ulong disconnectedAt = 0;
        private const int reconnectAfter = 50000000;  // 5 seconds

        // color image
        private byte[] colorImageData = null;
        private ulong lastColorImageTime = 0;

        // infrared image
        private int infraredImageWidth = 0, infraredImageHeight = 0;
        private byte[] infraredImageData = null;
        private ulong lastInfraredImageTime = 0;
        private Texture2D infraredImageTex = null;

        // data frame times
        private ulong lastControlFrameTime = 0;
        private ulong lastColorFrameTime = 0;
        private ulong lastDepthFrameTime = 0;
        private ulong lastInfraredFrameTime = 0;
        private ulong lastBodyDataFrameTime = 0;
        private ulong lastBodyIndexFrameTime = 0;
        private ulong lastPoseFrameTime = 0;
        private ulong lastDepth2colorFrameTime = 0;
        private ulong lastColor2depthFrameTime = 0;
        private ulong lastColor2bodyIndexFrameTime = 0;

        // body frame
        private bool bIgnoreZcoords = false;

        // pose frame
        private KinectInterop.NetPoseData netPoseData = null;
        private object netPoseLock = new object();

        // depth2color frame
        private int depth2colorImageWidth = 0, depth2colorImageHeight = 0;
        private byte[] depth2colorImageData = null;
        private ulong lastDepth2ColorImageTime = 0;

        // decompressors
        private ILZ4Decompressor controlFrameDecompressor = null;
        private ILZ4Decompressor depthFrameDecompressor = null;
        private ILZ4Decompressor bodyIndexFrameDecompressor = null;
        private ILZ4Decompressor color2depthFrameDecompressor = null;
        private ILZ4Decompressor color2bodyIndexFrameDecompressor = null;

        // console buffer
        private System.Text.StringBuilder sbConsole = new System.Text.StringBuilder();


        // depth sensor settings
        [System.Serializable]
        public class NetSensorSettings : DepthSensorBase.BaseSensorSettings
        {
            public string serverHost;
            public int serverBasePort;
            public bool getBodyIndexFrames;
        }


        public override KinectInterop.DepthSensorPlatform GetSensorPlatform()
        {
            return KinectInterop.DepthSensorPlatform.NetSensor;
        }


        public override BaseSensorSettings GetSensorSettings(BaseSensorSettings settings)
        {
            if (settings == null)
            {
                settings = new NetSensorSettings();
            }

            NetSensorSettings extSettings = (NetSensorSettings)base.GetSensorSettings(settings);
            extSettings.serverHost = serverHost;
            extSettings.serverBasePort = serverBasePort;
            extSettings.getBodyIndexFrames = getBodyIndexFrames;

            return settings;
        }

        public override void SetSensorSettings(BaseSensorSettings settings)
        {
            if (settings == null)
                return;

            base.SetSensorSettings(settings);

            NetSensorSettings extSettings = (NetSensorSettings)settings;
            serverHost = extSettings.serverHost;
            serverBasePort = extSettings.serverBasePort;
            getBodyIndexFrames = extSettings.getBodyIndexFrames;
        }

        public override List<KinectInterop.SensorDeviceInfo> GetAvailableSensors()
        {
            List<KinectInterop.SensorDeviceInfo> alSensorInfo = new List<KinectInterop.SensorDeviceInfo>();

            KinectInterop.SensorDeviceInfo sensorInfo = new KinectInterop.SensorDeviceInfo();
            sensorInfo.sensorId = serverHost + ":" + serverBasePort;
            sensorInfo.sensorName = "NetSensor";

            sensorInfo.sensorCaps = KinectInterop.FrameSource.TypeAll;

            Debug.Log(string.Format("  D{0}: {1}, id: {2}", 0, sensorInfo.sensorName, sensorInfo.sensorId));

            alSensorInfo.Add(sensorInfo);

            return alSensorInfo;
        }

        public override KinectInterop.SensorData OpenSensor(KinectInterop.FrameSource dwFlags, bool bSyncDepthAndColor, bool bSyncBodyAndDepth)
        {
            // save initial parameters
            base.OpenSensor(dwFlags, bSyncDepthAndColor, bSyncBodyAndDepth);

            if (deviceStreamingMode == KinectInterop.DeviceStreamingMode.PlayRecording)
            {
                Debug.LogWarning("Playback selected, but this is not applicable to the network sensor. Ignoring...");
            }

            List<KinectInterop.SensorDeviceInfo> alSensors = GetAvailableSensors();
            if (deviceIndex >= alSensors.Count)
            {
                Debug.Log("  D" + deviceIndex + " is not available. You can set the device index to -1, to disable it.");
                return null;
            }

            sensorData = new KinectInterop.SensorData();

            // flip color & depth image vertically
            sensorData.colorImageScale = new Vector3(-1f, -1f, 1f);
            sensorData.depthImageScale = new Vector3(-1f, -1f, 1f);
            sensorData.infraredImageScale = new Vector3(-1f, -1f, 1f);
            sensorData.sensorSpaceScale = new Vector3(-1f, -1f, 1f);

            // depth camera offset & matrix z-flip
            sensorRotOffset = new Vector3(0f, 0f, 0f);
            sensorRotFlipZ = true;
            sensorRotIgnoreY = true;

            // color camera data & intrinsics
            sensorData.colorImageFormat = TextureFormat.RGB24;
            sensorData.colorImageStride = 3;  // 3 bytes per pixel

            // init network clients
            InitNetClients(dwFlags);

            Debug.Log("NetSensor opened");

            return sensorData;
        }

        public override void CloseSensor(KinectInterop.SensorData sensorData)
        {
            // close network clients
            CloseNetClients();

            // close opened resources
            base.CloseSensor(sensorData);

            Debug.Log("NetSensor closed");
        }


        public override bool IsSensorDataValid()
        {
            // wait for net-cst
            long timeStart = System.DateTime.Now.Ticks;
            long timeNow = timeStart;

            while (!bGotNetSensorData && (timeNow - timeStart) < 50000000)  // timeout - 5 seconds
            {
                Thread.Sleep(10);
                timeNow = System.DateTime.Now.Ticks;
            }

            if (bGotNetSensorData && !bSetNetSensorData && netSensorData != null)
            {
                SetNetSensorData(netSensorData, sensorData, KinectManager.Instance);
                bSetNetSensorData = true;
                netSensorData = null;
            }

            return bGotNetSensorData;
        }


        public override bool UpdateSensorData(KinectInterop.SensorData sensorData, KinectManager kinectManager, bool isPlayMode)
        {
            // client status text
            if (sbConsole.Length > 0)
            {
                lock (sbConsole)
                {
                    if (clientStatusText)
                        clientStatusText.text = sbConsole.ToString();
                    sbConsole.Clear();
                }
            }

            // check for server timeout
            ulong ulTimeNow = (ulong)System.DateTime.Now.Ticks;
            bool isControlClientActive = controlFrameClient != null ? controlFrameClient.IsActive() : false;

            latestDataReceivedAt = GetLatestTimestamp();
            if(latestDataReceivedAt != 0 && (ulTimeNow - latestDataReceivedAt) >= disconnectAfter && isControlClientActive)
            {
                Debug.Log("Server timeout detected. Disconnecting...");

                disconnectedAt = ulTimeNow;
                CloseNetClients();
                return true;
            }

            // check for server disconnection
            if (disconnectedAt == 0 && controlFrameClient != null && !isControlClientActive)
            {
                Debug.Log("Server disconnection detected.");
                CloseNetClients();
                disconnectedAt = ulTimeNow;
            }

            // try to reconnect
            if(disconnectedAt != 0 && (ulTimeNow - disconnectedAt) >= reconnectAfter)
            {
                Debug.Log("Start reconnecting...");

                CloseNetClients();
                InitNetClients(frameSourceFlags);
                return true;
            }

            // send control messages as needed
            UpdateSendControlMessages(ulTimeNow, isControlClientActive, kinectManager);

            // color frame
            if (colorImageData != null && sensorData.lastColorFrameTime != lastColorImageTime && !isPlayMode)
            {
                if (sensorData.colorImageTexture == null && sensorData.colorImageWidth > 0 && sensorData.colorImageHeight > 0)
                {
                    sensorData.colorImageTexture = new Texture2D(sensorData.colorImageWidth, sensorData.colorImageHeight, TextureFormat.RGB24, false);
                    sensorData.colorImageTexture.wrapMode = TextureWrapMode.Clamp;
                    sensorData.colorImageTexture.filterMode = FilterMode.Point;
                }

                lock (colorFrameLock)
                {
                    Texture2D colorImageTex2D = (Texture2D)sensorData.colorImageTexture;
                    if (colorImageTex2D != null)
                    {
                        colorImageTex2D.LoadImage(colorImageData);
                        colorImageTex2D.Apply();
                    }

                    sensorData.lastColorFrameTime = currentColorTimestamp = rawColorTimestamp = lastColorImageTime;
                    //Debug.Log("UpdateColorTimestamp: " + lastColorImageTime);
                }
            }

            // check for depth texture
            if((sensorData.depthImageTexture == null && sensorData.depthImage != null && 
                    kinectManager.getDepthFrames == KinectManager.DepthTextureType.DepthTexture) ||
                (sensorData.bodyImageTexture == null && sensorData.bodyIndexImage != null && 
                    (kinectManager.getBodyFrames == KinectManager.BodyTextureType.UserTexture || kinectManager.getBodyFrames == KinectManager.BodyTextureType.BodyTexture)))
            {
                KinectInterop.InitSensorData(sensorData, kinectManager);
            }

            // infrared frame
            if(infraredImageData != null && sensorData.lastInfraredImageTime != lastInfraredImageTime && !isPlayMode)
            {
                if (sensorData.infraredImageTexture == null && infraredImageWidth > 0 && infraredImageHeight > 0)
                {
                    infraredImageTex = new Texture2D(infraredImageWidth, infraredImageHeight, TextureFormat.RGB24, false);
                    infraredImageTex.wrapMode = TextureWrapMode.Clamp;
                    infraredImageTex.filterMode = FilterMode.Point;

                    sensorData.infraredImageTexture = KinectInterop.CreateRenderTexture(sensorData.infraredImageTexture, infraredImageWidth, infraredImageHeight);
                }

                lock (infraredFrameLock)
                {
                    if (infraredImageTex != null)
                    {
                        infraredImageTex.LoadImage(infraredImageData);
                        infraredImageTex.Apply();

                        Graphics.Blit(infraredImageTex, sensorData.infraredImageTexture);
                    }

                    sensorData.lastInfraredImageTime = lastInfraredImageTime;
                    //Debug.Log("UpdateInfraredImageTimestamp: " + lastInfraredImageTime);
                }
            }

            // pose frame
            if (netPoseData != null && sensorData.lastSensorPoseFrameTime != netPoseData.sensorPoseTime && !isPlayMode)
            {
                lock(netPoseLock)
                {
                    SetSensorNetPoseData(netPoseData, sensorData, kinectManager);
                    ApplySensorPoseUpdate(kinectManager);
                }
            }

            // get KM setting for processing of body data
            bIgnoreZcoords = kinectManager.ignoreZCoordinates;

            // process the other sensor data
            return base.UpdateSensorData(sensorData, kinectManager, isPlayMode);
        }

        // send the control messages as needed
        private void UpdateSendControlMessages(ulong ulTimeNow, bool isControlClientActive, KinectManager kinectManager)
        {
            //// control - get sensor data
            //if (!bSensorDataMsgSent && isControlClientActive)
            //{
            //    SendControlMessage(ControlMessageType.GetSensorData);

            //    bSensorDataMsgSent = true;
            //    lastKeepAliveFrameTime = ulTimeNow;
            //}

            // control - set-sensor-data
            if (netSensorData != null)
            {
                SetNetSensorData(netSensorData, sensorData, kinectManager);
                bSetNetSensorData = true;
                netSensorData = null;
            }

            // control - keep alive
            if ((ulTimeNow - lastKeepAliveFrameTime) >= keepAliveInterval && isControlClientActive)
            {
                lastKeepAliveFrameTime = ulTimeNow;
                SendControlMessage(ControlMessageType.KeepAlive);
            }
        }


        // unprojects plane point into the space
        protected override Vector3 UnprojectPoint(KinectInterop.CameraIntrinsics intr, Vector2 pixel, float depth)
        {
            Vector3 point = Vector3.zero;
            if (intr == null || depth <= 0f)
                return point;

            if(sensorPlatform == KinectInterop.DepthSensorPlatform.Kinect4Azure || sensorPlatform == KinectInterop.DepthSensorPlatform.DummyK4A)
            {
                depth = depth * 1000f;
            }

            int di = (int)(pixel.x + 0.5f) + (int)(pixel.y + 0.5f) * intr.width;
            if (intr.cameraType == 0) // depth
            {
                if (depth2SpaceTable == null)
                {
                    GetDepthCameraSpaceTable(sensorData);
                }

                if(depth2SpaceTable != null && di >= 0 && di < depth2SpaceTable.Length)
                {
                    point = depth2SpaceTable[di] * depth;
                }
            }
            else if(intr.cameraType == 1)  // color
            {
                if (color2SpaceTable == null)
                {
                    GetColorCameraSpaceTable(sensorData);
                }

                if (color2SpaceTable != null && di >= 0 && di < color2SpaceTable.Length)
                {
                    point = color2SpaceTable[di] * depth;
                }
            }

            return point;
        }


        // projects space point onto a plane
        protected override Vector2 ProjectPoint(KinectInterop.CameraIntrinsics intr, Vector3 point)
        {
            if (intr == null || point == Vector3.zero)
                return Vector2.zero;

            float x = point.x / point.z;
            float y = point.y / point.z;

            Vector2 pixel = new Vector2(x * intr.fx + intr.ppx, y * intr.fy + intr.ppy);

            return pixel;
        }


        // transforms a point from one space to another
        protected override Vector3 TransformPoint(KinectInterop.CameraExtrinsics extr, Vector3 point)
        {
            if (extr == null)
                return Vector3.zero;

            float toPointX = 0f, toPointY = 0f, toPointZ = 0f;
            if (sensorPlatform == KinectInterop.DepthSensorPlatform.RealSense)
            {
                // RS
                toPointX = extr.rotation[0] * point.x + extr.rotation[3] * point.y + extr.rotation[6] * point.z + extr.translation[0];
                toPointY = extr.rotation[1] * point.x + extr.rotation[4] * point.y + extr.rotation[7] * point.z + extr.translation[1];
                toPointZ = extr.rotation[2] * point.x + extr.rotation[5] * point.y + extr.rotation[8] * point.z + extr.translation[2];
            }
            else
            {
                // K4A, K2
                toPointX = extr.rotation[0] * point.x + extr.rotation[1] * point.y + extr.rotation[2] * point.z + extr.translation[0];
                toPointY = extr.rotation[3] * point.x + extr.rotation[4] * point.y + extr.rotation[5] * point.z + extr.translation[1];
                toPointZ = extr.rotation[6] * point.x + extr.rotation[7] * point.y + extr.rotation[8] * point.z + extr.translation[2];
            }

            return new Vector3(toPointX, toPointY, toPointZ);
        }


        public override Vector3[] GetDepthCameraSpaceTable(KinectInterop.SensorData sensorData)
        {
            if (sensorData == null)
                return null;

            if (depth2SpaceTable == null)
            {
                int depthImageLength = sensorData.depthImageWidth * sensorData.depthImageHeight;
                depth2SpaceTable = new Vector3[depthImageLength];
                //bNeedDST = true;

                SendControlMessage(ControlMessageType.GetDST);
                //bDSTsent = true;
            }

            // wait for net-dst
            long timeStart = System.DateTime.Now.Ticks;
            long timeNow = timeStart;

            while (!bGotDST && (timeNow - timeStart) < 50000000)  // timeout - 5 seconds
            {
                Thread.Sleep(10);
                timeNow = System.DateTime.Now.Ticks;
            }

            if(!bGotDST)
            {
                Debug.LogWarning("Timed out waiting for net-dst.");
            }

            return depth2SpaceTable;
        }


        public override Vector3[] GetColorCameraSpaceTable(KinectInterop.SensorData sensorData)
        {
            if (sensorData == null)
                return null;

            if (color2SpaceTable == null)
            {
                int colorImageLength = sensorData.colorImageWidth * sensorData.colorImageHeight;
                color2SpaceTable = new Vector3[colorImageLength];
                //bNeedCST = true;

                SendControlMessage(ControlMessageType.GetCST);
                //bCSTsent = true;
            }

            // wait for net-cst
            long timeStart = System.DateTime.Now.Ticks;
            long timeNow = timeStart;

            while (!bGotCST && (timeNow - timeStart) < 50000000)  // timeout - 5 seconds
            {
                Thread.Sleep(10);
                timeNow = System.DateTime.Now.Ticks;
            }

            if (!bGotCST)
            {
                Debug.LogWarning("Timed out waiting for net-cst.");
            }

            return color2SpaceTable;
        }


        // returns the point cloud texture resolution
        public override Vector2Int GetPointCloudTexResolution(KinectInterop.SensorData sensorData)
        {
            Vector2Int texRes = Vector2Int.zero;

            // wait for net-cst
            long timeStart = System.DateTime.Now.Ticks;
            long timeNow = timeStart;

            while (texRes == Vector2Int.zero && (timeNow - timeStart) < 50000000)  // timeout - 5 seconds
            {
                switch (pointCloudResolution)
                {
                    case PointCloudResolution.DepthCameraResolution:
                        texRes = new Vector2Int(sensorData.depthImageWidth, sensorData.depthImageHeight);
                        break;

                    case PointCloudResolution.ColorCameraResolution:
                        texRes = new Vector2Int(sensorData.colorImageWidth, sensorData.colorImageHeight);
                        break;
                }

                if(texRes == Vector2Int.zero)
                {
                    Thread.Sleep(10);
                }

                timeNow = System.DateTime.Now.Ticks;
            }

            if (texRes == Vector2Int.zero)
            {
                throw new System.Exception("Unsupported point cloud resolution: " + pointCloudResolution + " or the respective image is not available.");
            }

            return texRes;
        }


        // creates the point-cloud vertex shader and its respective buffers, as needed
        protected override bool CreatePointCloudVertexShader(KinectInterop.SensorData sensorData)
        {
            bool bSuccess = base.CreatePointCloudVertexShader(sensorData);

            if (pointCloudResolution == PointCloudResolution.ColorCameraResolution && color2depthFrameClient == null)
            {
                color2depthFrameDecompressor = LZ4DecompressorFactory.CreateNew();

                color2depthFrameClient = new TcpNetClient(sbConsole, color2depthFrameDecompressor);
                color2depthFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.Color2Depth, "tdepth", null);
                color2depthFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(Color2DepthFrameReceived);
            }

            return bSuccess;
        }


        // disposes the point-cloud vertex shader and its respective buffers
        protected override void DisposePointCloudVertexShader(KinectInterop.SensorData sensorData)
        {
            base.DisposePointCloudVertexShader(sensorData);

            if(color2depthFrameClient != null)
            {
                color2depthFrameClient.ReceivedMessage -= Color2DepthFrameReceived;
                color2depthFrameClient.Close();
                color2depthFrameClient = null;
            }
        }


        // creates the point-cloud color shader and its respective buffers, as needed
        protected override bool CreatePointCloudColorShader(KinectInterop.SensorData sensorData)
        {
            if (pointCloudResolution == PointCloudResolution.DepthCameraResolution)
            {
                if (pointCloudAlignedColorTex == null)
                {
                    pointCloudAlignedColorTex = new Texture2D(sensorData.depthImageWidth, sensorData.depthImageHeight, TextureFormat.RGB24, false);
                    pointCloudAlignedColorTex.wrapMode = TextureWrapMode.Clamp;
                    pointCloudAlignedColorTex.filterMode = FilterMode.Point;
                }

                if (depth2colorFrameClient == null)
                {
                    depth2colorFrameClient = new TcpNetClient(sbConsole, null);
                    depth2colorFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.Depth2Color, "tcolor", null);
                    depth2colorFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(Depth2ColorFrameReceived);
                }

                return true;
            }
            else
            {
                return base.CreatePointCloudColorShader(sensorData);
            }
        }


        // disposes the point-cloud color shader and its respective buffers
        protected override void DisposePointCloudColorShader(KinectInterop.SensorData sensorData)
        {
            if(depth2colorFrameClient != null)
            {
                depth2colorFrameClient.ReceivedMessage -= Depth2ColorFrameReceived;
                depth2colorFrameClient.Close();
                depth2colorFrameClient = null;

                pointCloudAlignedColorTex = null;
            }
            else
            {
                base.DisposePointCloudColorShader(sensorData);
            }
        }


        // updates the point-cloud color shader with the actual data
        protected override bool UpdatePointCloudColorShader(KinectInterop.SensorData sensorData)
        {
            if (depth2colorFrameClient != null)
            {
                // depth2color frame
                if (depth2colorImageData != null && sensorData.lastDepth2ColorFrameTime != lastDepth2ColorImageTime)
                {
                    if (depth2colorImageWidth > 0 && depth2colorImageHeight > 0)
                    {
                        if (pointCloudAlignedColorTex == null || pointCloudAlignedColorTex.width != depth2colorImageWidth || pointCloudAlignedColorTex.height != depth2colorImageHeight)
                        {
                            pointCloudAlignedColorTex = new Texture2D(depth2colorImageWidth, depth2colorImageHeight, TextureFormat.RGB24, false);
                            pointCloudAlignedColorTex.wrapMode = TextureWrapMode.Clamp;
                            pointCloudAlignedColorTex.filterMode = FilterMode.Point;
                        }
                    }

                    lock (depthCamColorFrameLock)
                    {
                        if (pointCloudAlignedColorTex != null)
                        {
                            pointCloudAlignedColorTex.LoadImage(depth2colorImageData);
                            pointCloudAlignedColorTex.Apply();

                            Graphics.Blit(pointCloudAlignedColorTex, pointCloudColorTexture);
                        }

                        sensorData.lastDepth2ColorFrameTime = lastDepthCamColorFrameTime = lastDepth2ColorImageTime;
                        //Debug.Log("UpdateDepth2ColorImageTimestamp: " + lastDepth2ColorImageTime);
                    }

                    if (pointCloudColorRT != null)
                    {
                        Graphics.CopyTexture(pointCloudColorTexture, pointCloudColorRT);
                    }
                }

                return true;
            }
            else
            {
                return base.UpdatePointCloudColorShader(sensorData);
            }
        }


        // creates the color-cam depth shader and its respective buffers, as needed
        protected override bool CreateColorDepthShader(KinectInterop.SensorData sensorData)
        {
            bool bSuccess = base.CreateColorDepthShader(sensorData);

            if (color2depthFrameClient == null)
            {
                color2depthFrameDecompressor = LZ4DecompressorFactory.CreateNew();

                color2depthFrameClient = new TcpNetClient(sbConsole, color2depthFrameDecompressor);
                color2depthFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.Color2Depth, "tdepth", null);
                color2depthFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(Color2DepthFrameReceived);
            }

            return bSuccess;
        }


        // disposes the color-cam depth shader and its respective buffers
        protected override void DisposeColorDepthShader(KinectInterop.SensorData sensorData)
        {
            base.DisposeColorDepthShader(sensorData);

            if (color2depthFrameClient != null)
            {
                color2depthFrameClient.ReceivedMessage -= Color2DepthFrameReceived;
                color2depthFrameClient.Close();
                color2depthFrameClient = null;
            }
        }


        // creates the color-cam body-index shader and its respective buffers, as needed
        protected override bool CreateColorBodyIndexShader(KinectInterop.SensorData sensorData)
        {
            bool bSuccess = base.CreateColorBodyIndexShader(sensorData);

            if (color2bodyIndexFrameClient == null)
            {
                color2bodyIndexFrameDecompressor = LZ4DecompressorFactory.CreateNew();

                color2bodyIndexFrameClient = new TcpNetClient(sbConsole, color2bodyIndexFrameDecompressor);
                color2bodyIndexFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.Color2BodyIndex, "tbodyindex", null);
                color2bodyIndexFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(Color2BodyIndexFrameReceived);
            }

            return bSuccess;
        }


        // disposes the color-cam body-index shader and its respective buffers
        protected override void DisposeColorBodyIndexShader(KinectInterop.SensorData sensorData)
        {
            base.DisposeColorBodyIndexShader(sensorData);

            if (color2bodyIndexFrameClient != null)
            {
                color2bodyIndexFrameClient.ReceivedMessage -= Color2BodyIndexFrameReceived;
                color2bodyIndexFrameClient.Close();
                color2bodyIndexFrameClient = null;
            }
        }


        // initializes the network clients
        private void InitNetClients(KinectInterop.FrameSource dwFlags)
        {
            try
            {
                // clear params
                //bSensorDataMsgSent = false;
                bGotNetSensorData = false;
                bSetNetSensorData = false;

                lastKeepAliveFrameTime = 0;
                latestDataReceivedAt = 0;
                disconnectedAt = 0;

                lastControlFrameTime = 0;
                lastColorFrameTime = 0;
                lastDepthFrameTime = 0;
                lastInfraredFrameTime = 0;
                lastBodyDataFrameTime = 0;
                lastBodyIndexFrameTime = 0;
                lastPoseFrameTime = 0;
                lastDepth2colorFrameTime = 0;
                lastColor2depthFrameTime = 0;
                lastColor2bodyIndexFrameTime = 0;

                // init clients
                controlFrameDecompressor = LZ4DecompressorFactory.CreateNew();
                controlFrameClient = new TcpNetClient(sbConsole, controlFrameDecompressor);

                NetMessageData msgGetData = GetControlMessage(ControlMessageType.GetSensorData);
                controlFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.Control, "control", msgGetData);
                controlFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(ControlFrameReceived);

                if ((dwFlags & KinectInterop.FrameSource.TypeColor) != 0)
                {
                    colorFrameClient = new TcpNetClient(sbConsole, null);
                    colorFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.Color, "color", null);
                    colorFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(ColorFrameReceived);
                }

                if ((dwFlags & KinectInterop.FrameSource.TypeDepth) != 0)
                {
                    depthFrameDecompressor = LZ4DecompressorFactory.CreateNew();

                    depthFrameClient = new TcpNetClient(sbConsole, depthFrameDecompressor);
                    depthFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.Depth, "depth", null);
                    depthFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(DepthFrameReceived);
                }

                if ((dwFlags & KinectInterop.FrameSource.TypeInfrared) != 0)
                {
                    infraredFrameClient = new TcpNetClient(sbConsole, null);
                    infraredFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.Infrared, "infrared", null);
                    infraredFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(InfraredFrameReceived);
                }

                if ((dwFlags & KinectInterop.FrameSource.TypeBody) != 0)
                {
                    bodyDataFrameClient = new TcpNetClient(sbConsole, null);
                    bodyDataFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.BodyData, "body-data", null);
                    bodyDataFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(BodyDataFrameReceived);
                }

                if ((dwFlags & KinectInterop.FrameSource.TypeBodyIndex) != 0 && getBodyIndexFrames)
                {
                    bodyIndexFrameDecompressor = LZ4DecompressorFactory.CreateNew();

                    bodyIndexFrameClient = new TcpNetClient(sbConsole, bodyIndexFrameDecompressor);
                    bodyIndexFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.BodyIndex, "body-index", null);
                    bodyIndexFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(BodyIndexFrameReceived);
                }

                if ((dwFlags & KinectInterop.FrameSource.TypePose) != 0)
                {
                    poseFrameClient = new TcpNetClient(sbConsole, null);
                    poseFrameClient.ConnectToServer(serverHost, serverBasePort + (int)NetMessageType.Pose, "pose", null);
                    poseFrameClient.ReceivedMessage += new ReceivedMessageEventHandler(PoseFrameReceived);
                }

            }
            catch (System.Exception ex)
            {
                Debug.LogError("Error while initing the net clients.");
                Debug.LogException(ex);
            }
        }


        // closes all network clients
        private void CloseNetClients()
        {
            try
            {
                if (controlFrameClient != null)
                {
                    SendControlMessage(ControlMessageType.Disconnect);

                    controlFrameClient.ReceivedMessage -= ControlFrameReceived;
                    controlFrameClient.Close();
                    controlFrameClient = null;
                    controlFrameDecompressor = null;
                }

                if (colorFrameClient != null)
                {
                    colorFrameClient.ReceivedMessage -= ColorFrameReceived;
                    colorFrameClient.Close();
                    colorFrameClient = null;
                }

                if (depthFrameClient != null)
                {
                    depthFrameClient.ReceivedMessage -= DepthFrameReceived;
                    depthFrameClient.Close();
                    depthFrameClient = null;
                    depthFrameDecompressor = null;
                }

                if (infraredFrameClient != null)
                {
                    infraredFrameClient.ReceivedMessage -= InfraredFrameReceived;
                    infraredFrameClient.Close();
                    infraredFrameClient = null;
                }

                if (bodyDataFrameClient != null)
                {
                    bodyDataFrameClient.ReceivedMessage -= BodyDataFrameReceived;
                    bodyDataFrameClient.Close();
                    bodyDataFrameClient = null;
                }

                if (bodyIndexFrameClient != null)
                {
                    bodyIndexFrameClient.ReceivedMessage -= BodyIndexFrameReceived;
                    bodyIndexFrameClient.Close();
                    bodyIndexFrameClient = null;
                    bodyIndexFrameDecompressor = null;
                }

                if (poseFrameClient != null)
                {
                    poseFrameClient.ReceivedMessage -= PoseFrameReceived;
                    poseFrameClient.Close();
                    poseFrameClient = null;
                }

                if (depth2colorFrameClient != null)
                {
                    depth2colorFrameClient.ReceivedMessage -= Depth2ColorFrameReceived;
                    depth2colorFrameClient.Close();
                    depth2colorFrameClient = null;
                }

                if (color2depthFrameClient != null)
                {
                    color2depthFrameClient.ReceivedMessage -= Color2DepthFrameReceived;
                    color2depthFrameClient.Close();
                    color2depthFrameClient = null;
                    color2depthFrameDecompressor = null;
                }

                if (color2bodyIndexFrameClient != null)
                {
                    color2bodyIndexFrameClient.ReceivedMessage -= Color2BodyIndexFrameReceived;
                    color2bodyIndexFrameClient.Close();
                    color2bodyIndexFrameClient = null;
                    color2bodyIndexFrameDecompressor = null;
                }

            }
            catch (System.Exception ex)
            {
                Debug.LogError("Error while closing the net servers.");
                Debug.LogException(ex);
            }
        }


        private void ControlFrameReceived(object state, ReceivedMessageEventArgs args)
        {
            if (sensorData == null)
                return;

            try
            {
                // ignore keep-alives from the server
                if(args.message.frameData.Length > 2)
                {
                    switch(args.message.encType)
                    {
                        case FrameEncodeType.SensorDataJson:  // get-sensor-data
                            CtrlGotNetSensorData(args.message.frameData);
                            break;

                        case FrameEncodeType.DSTraw:  // get-dst
                            CtrlGotNetDST(args.message.frameData);
                            break;

                        case FrameEncodeType.CSTraw:  // get-cst
                            CtrlGotNetCST(args.message.frameData);
                            break;
                    }
                }

                lastControlFrameTime = (ulong)System.DateTime.Now.Ticks;
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        // process net-sensor-data bytes
        private void CtrlGotNetSensorData(byte[] btData)
        {
            string sNetSensorData = System.Text.Encoding.UTF8.GetString(btData);
            netSensorData = JsonUtility.FromJson<KinectInterop.NetSensorData>(sNetSensorData);
            //System.IO.File.WriteAllText("NetSensorData.json", sNetSensorData);
            bGotNetSensorData = true;

            //lastControlFrameTime = args.message.timestamp;
            Debug.Log("Got net-sensor-data");
        }

        // process net-dst bytes
        private void CtrlGotNetDST(byte[] btData)
        {
            if (depth2SpaceTable == null)
            {
                int depthImageLength = sensorData.depthImageWidth * sensorData.depthImageHeight;
                depth2SpaceTable = new Vector3[depthImageLength];
            }

            KinectInterop.CopyBytes(btData, 1, depth2SpaceTable, 3 * sizeof(float));
            //System.IO.File.WriteAllBytes("NetDST.bin", btData);
            //lastControlFrameTime = args.message.timestamp;

            bGotDST = true;
            Debug.Log("Got net-dst");
        }

        // process net-cst bytes
        private void CtrlGotNetCST(byte[] btData)
        {
            if (color2SpaceTable == null)
            {
                int colorImageLength = sensorData.colorImageWidth * sensorData.colorImageHeight;
                color2SpaceTable = new Vector3[colorImageLength];
            }

            KinectInterop.CopyBytes(btData, 1, color2SpaceTable, 3 * sizeof(float));
            //System.IO.File.WriteAllBytes("NetCST.bin", btData);
            //lastControlFrameTime = args.message.timestamp;

            bGotCST = true;
            Debug.Log("Got net-cst");
        }


        private void ColorFrameReceived(object state, ReceivedMessageEventArgs args)
        {
            if (sensorData == null)
                return;

            try
            {
                if (sensorData.colorImageWidth == 0)
                {
                    sensorData.colorImageWidth = args.message.frameWidth;
                    sensorData.colorImageHeight = args.message.frameHeight;
                }

                lock (colorFrameLock)
                {
                    colorImageData = args.message.frameData;
                    lastColorImageTime = args.message.timestamp;

                    lastColorFrameTime = (ulong)System.DateTime.Now.Ticks;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void DepthFrameReceived(object state, ReceivedMessageEventArgs args)
        {
            if (sensorData == null)
                return;

            try
            {
                if (sensorData.depthImage == null)
                {
                    sensorData.depthImageWidth = args.message.frameWidth;
                    sensorData.depthImageHeight = args.message.frameHeight;

                    rawDepthImage = new ushort[sensorData.depthImageWidth * sensorData.depthImageHeight];
                    sensorData.depthImage = new ushort[sensorData.depthImageWidth * sensorData.depthImageHeight];
                }

                lock (depthFrameLock)
                {
                    KinectInterop.CopyBytes(args.message.frameData, sizeof(byte), rawDepthImage, sizeof(ushort));
                    rawDepthTimestamp = args.message.timestamp;

                    lastDepthFrameTime = (ulong)System.DateTime.Now.Ticks;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void InfraredFrameReceived(object state, ReceivedMessageEventArgs args)
        {
            if (sensorData == null)
                return;

            try
            {
                if (infraredImageWidth == 0 || infraredImageHeight == 0)
                {
                    infraredImageWidth = args.message.frameWidth;
                    infraredImageHeight = args.message.frameHeight;
                }

                lock (infraredFrameLock)
                {
                    infraredImageData = args.message.frameData;
                    lastInfraredImageTime = args.message.timestamp;

                    lastInfraredFrameTime = (ulong)System.DateTime.Now.Ticks;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void BodyDataFrameReceived(object state, ReceivedMessageEventArgs args)
        {
            if (sensorData == null)
                return;

            try
            {
                if (sensorData.alTrackedBodies == null)
                {
                    sensorData.alTrackedBodies = new KinectInterop.BodyData[0];
                    sensorData.trackedBodiesCount = 0;
                }

                lock (bodyTrackerLock)
                {
                    Matrix4x4 sensorToWorld = GetSensorToWorldMatrix();

                    string sBodyFrameData = System.Text.Encoding.UTF8.GetString(args.message.frameData);
                    sensorData.trackedBodiesCount = KinectInterop.SetBodyFrameFromCsv(sBodyFrameData, "\t", ref sensorData.alTrackedBodies, 
                        ref sensorToWorld, bIgnoreZcoords, out rawBodyTimestamp);
                    sensorData.lastBodyFrameTime = currentBodyTimestamp = rawBodyTimestamp = args.message.timestamp;

                    lastBodyDataFrameTime = (ulong)System.DateTime.Now.Ticks;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void BodyIndexFrameReceived(object state, ReceivedMessageEventArgs args)
        {
            if (sensorData == null)
                return;

            try
            {
                if (sensorData.bodyIndexImage == null)
                {
                    //rawBodyIndexImage = new byte[args.message.frameWidth * args.message.frameHeight];
                    sensorData.bodyIndexImage = new byte[args.message.frameWidth * args.message.frameHeight];
                }

                lock (bodyTrackerLock)
                {
                    rawBodyIndexImage = args.message.frameData;
                    rawBodyIndexTimestamp = args.message.timestamp;

                    lastBodyIndexFrameTime = (ulong)System.DateTime.Now.Ticks;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }


        private void PoseFrameReceived(object state, ReceivedMessageEventArgs args)
        {
            if (sensorData == null)
                return;

            try
            {
                lock(netPoseLock)
                {
                    string sNetPoseData = System.Text.Encoding.UTF8.GetString(args.message.frameData);
                    netPoseData = JsonUtility.FromJson<KinectInterop.NetPoseData>(sNetPoseData);

                    lastPoseFrameTime = (ulong)System.DateTime.Now.Ticks;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }


        private void Depth2ColorFrameReceived(object state, ReceivedMessageEventArgs args)
        {
            if (sensorData == null)
                return;

            try
            {
                if (depth2colorImageWidth == 0 || depth2colorImageHeight == 0)
                {
                    depth2colorImageWidth = args.message.frameWidth;
                    depth2colorImageHeight = args.message.frameHeight;
                }

                lock (depthCamColorFrameLock)
                {
                    depth2colorImageData = args.message.frameData;
                    lastDepth2ColorImageTime = args.message.timestamp;

                    lastDepth2colorFrameTime = (ulong)System.DateTime.Now.Ticks;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }


        private void Color2DepthFrameReceived(object state, ReceivedMessageEventArgs args)
        {
            if (sensorData == null)
                return;

            try
            {
                if (colorCamDepthDataFrame == null || (args.message.frameWidth * args.message.frameHeight) != colorCamDepthDataFrame.Length)
                {
                    colorCamDepthDataFrame = new ushort[args.message.frameWidth * args.message.frameHeight];
                }

                lock (colorCamDepthFrameLock)
                {
                    KinectInterop.CopyBytes(args.message.frameData, sizeof(byte), colorCamDepthDataFrame, sizeof(ushort));
                    lastColorCamDepthFrameTime = args.message.timestamp;

                    lastColor2depthFrameTime = (ulong)System.DateTime.Now.Ticks;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }


        private void Color2BodyIndexFrameReceived(object state, ReceivedMessageEventArgs args)
        {
            if (sensorData == null)
                return;

            try
            {
                if (colorCamBodyIndexFrame == null || (args.message.frameWidth * args.message.frameHeight) != colorCamBodyIndexFrame.Length)
                {
                    colorCamBodyIndexFrame = new byte[args.message.frameWidth * args.message.frameHeight];
                }

                lock (colorCamBodyIndexFrameLock)
                {
                    KinectInterop.CopyBytes(args.message.frameData, sizeof(byte), colorCamBodyIndexFrame, sizeof(byte));
                    lastColorCamBodyIndexFrameTime = args.message.timestamp;

                    lastColor2bodyIndexFrameTime = (ulong)System.DateTime.Now.Ticks;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }


        // constructs and returns the control message
        private NetMessageData GetControlMessage(ControlMessageType messageType)
        {
            ulong ulTimeNow = (ulong)System.DateTime.Now.Ticks;
            NetMessageData msg = new NetMessageData(NetMessageType.Control, FrameEncodeType.Raw, 0, 0, ulTimeNow);

            byte[] btMsgData = new byte[1];
            btMsgData[0] = (byte)messageType;
            msg.SetData(btMsgData);

            return msg;
        }


        // sends control message to the server
        private void SendControlMessage(ControlMessageType messageType)
        {
            if(controlFrameClient != null && controlFrameClient.IsActive())
            {
                NetMessageData msg = GetControlMessage(messageType);

                controlFrameClient.SendMessageToAllConnections(msg);
                //Debug.Log("Sending ctrl message " + messageType);
            }
        }


        // return the latest timestamp
        private ulong GetLatestTimestamp()
        {
            ulong maxFrameTime = lastControlFrameTime;

            if (lastColorFrameTime != 0 && lastColorFrameTime > maxFrameTime)
                maxFrameTime = lastColorFrameTime;
            if (lastDepthFrameTime != 0 && lastDepthFrameTime > maxFrameTime)
                maxFrameTime = lastDepthFrameTime;
            if (lastInfraredFrameTime != 0 && lastInfraredFrameTime > maxFrameTime)
                maxFrameTime = lastInfraredFrameTime;
            if (lastBodyDataFrameTime != 0 && lastBodyDataFrameTime > maxFrameTime)
                maxFrameTime = lastBodyDataFrameTime;
            if (lastBodyIndexFrameTime != 0 && lastBodyIndexFrameTime > maxFrameTime)
                maxFrameTime = lastBodyIndexFrameTime;
            if (lastPoseFrameTime != 0 && lastPoseFrameTime > maxFrameTime)
                maxFrameTime = lastPoseFrameTime;
            if (lastDepth2colorFrameTime != 0 && lastDepth2colorFrameTime > maxFrameTime)
                maxFrameTime = lastDepth2colorFrameTime;
            if (lastColor2depthFrameTime != 0 && lastColor2depthFrameTime > maxFrameTime)
                maxFrameTime = lastColor2depthFrameTime;
            if (lastColor2bodyIndexFrameTime != 0 && lastColor2bodyIndexFrameTime > maxFrameTime)
                maxFrameTime = lastColor2bodyIndexFrameTime;

            return maxFrameTime;
        }

    }


    /// <summary>
    /// TcpNetClient provides network client functionality over tcp streams.
    /// This class is based on the excellent work of Andy Wilson and Hrvoje Benko in "Room Alive Toolkit" - https://github.com/microsoft/RoomAliveToolkit
    /// </summary>
    internal class TcpNetClient
    {
        //private const int bufferSize = 10240000;
        public int tmpBufferSize = 640000;

        private System.Text.StringBuilder sbConsole = null;
        private ILZ4Decompressor decompressor = null;

        private string streamDesc = string.Empty;
        private List<NetConnData> connections = new List<NetConnData>();
        private NetMessageData sendMsgOnConnect = null;

        // thread signal
        public ManualResetEvent allDoneClient = new ManualResetEvent(false);

        public ReceivedMessageEventHandler ReceivedMessage = null;


        public bool IsConnected()
        {
            return (connections.Count > 0);
        }

        public int GetConnCount()
        {
            return connections.Count;
        }

        public bool IsActive()
        {
            bool bActive = (connections.Count > 0) ? true : false;

            foreach (NetConnData conn in connections)
            {
                if (!conn.isActive)
                {
                    bActive = false;
                    break;
                }
            }

            return bActive;
        }

        public bool IsReadyToSend()
        {
            bool bReady = (connections.Count > 0) ? true : false;

            foreach (NetConnData conn in connections)
            {
                if (!conn.readyToSend)
                {
                    bReady = false;
                    break;
                }
            }

            return bReady;
        }

        //public void CloseConnection(int connId)
        //{
        //    foreach (NetConnData conn in connections)
        //    {
        //        if (conn.ID == connId)
        //        {
        //            //Debug.Log("Closing connection " + conn.ID + " to " + conn.remoteEP);
        //            LogToConsole("Closing connection to " + streamDesc + " server.");
        //            conn.isActive = false;

        //            connections.Remove(conn);
        //            break;
        //        }
        //    }
        //}

        public void CloseConnectionsTo(string remoteIP)
        {
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                NetConnData conn = connections[i];

                if (conn.remoteIP == remoteIP)
                {
                    //Debug.Log("Closing connection " + conn.ID + " to " + conn.remoteEP);
                    LogToConsole("Closing connection to " + streamDesc + " server.");
                    conn.isActive = false;

                    connections.Remove(conn);
                }
            }
        }

        public void CloseAllConnections()
        {
            foreach (NetConnData conn in connections)
            {
                //Debug.Log("Closing connection " + conn.ID + " to " + conn.remoteEP);
                LogToConsole("Closing connection to " + streamDesc + " server.");
                conn.isActive = false;
            }

            connections.Clear();
        }

        public void SendMessageToAllConnections(NetMessageData message)
        {
            foreach (NetConnData conn in connections)
            {
                lock (conn.messageQueue)
                {
                    if (conn.messageQueue.Count < NetConnData.MaxMessageQueueLength)
                    {
                        conn.messageQueue.Enqueue(message);
                    }
                }
            }
        }

        public void SendMessageToConnection(NetMessageData message, int connId)
        {
            foreach (NetConnData conn in connections)
            {
                if (conn.ID == connId)
                {
                    lock (conn.messageQueue)
                    {
                        if (conn.messageQueue.Count < NetConnData.MaxMessageQueueLength)
                        {
                            conn.messageQueue.Enqueue(message);
                        }
                    }

                    break;
                }
            }
        }


        public TcpNetClient(System.Text.StringBuilder sbConsole, ILZ4Decompressor decompressor)
        {
            this.sbConsole = sbConsole;
            this.decompressor = decompressor;
        }


        public void Close()
        {
            allDoneClient.Set();

            foreach (NetConnData cs in connections)
            {
                cs.isActive = false;
            }
        }

        public void ConnectToServer(string host, int port, string streamDesc, NetMessageData msgOnConnect)
        {
            this.streamDesc = streamDesc;
            this.sendMsgOnConnect = msgOnConnect;

            try
            {
                TcpClient client = new TcpClient(AddressFamily.InterNetwork);

                IPAddress ipAddress = null;
                bool isIpAddr = host.Length > 0 && host[0] >= '0' && host[0] <= '9';

                if (isIpAddr)
                {
                    ipAddress = IPAddress.Parse(host);
                }
                else
                {
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(host);
                    ipAddress = ipHostInfo.AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork).First();
                }

                //Debug.Log("Connecting to " + host + " - " + ipAddress + ":" + port);
                LogToConsole("Connecting to " + streamDesc + " server at " + ipAddress + ":" + port);
                client.BeginConnect(ipAddress, port, new System.AsyncCallback(HandleNetServerConnected), client);
            }
            catch (System.Exception ex)
            {
                //Debug.LogError("Connection to server failed: " + ex.Message);
                LogErrorToConsole("Connection to " + streamDesc + " server failed: " + ex.Message);
                //Debug.LogException(ex);
            }
        }

        private void HandleNetServerConnected(System.IAsyncResult ar)
        {
            try
            {
                NetConnData conn = new NetConnData();

                conn.client = (TcpClient)ar.AsyncState;
                conn.client.EndConnect(ar);
                conn.stream = conn.client.GetStream();

                conn.remoteIP = conn.client.Client.RemoteEndPoint.ToString();
                int iP = conn.remoteIP.IndexOf(':');
                if (iP >= 0)
                    conn.remoteIP = conn.remoteIP.Substring(0, iP);

                connections.Add(conn);
                //Debug.Log("Connected to server " + conn.remoteEP);
                LogToConsole("Connected to " + streamDesc + " server " + conn.remoteIP);

                if(sendMsgOnConnect != null)
                {
                    SendMessageToConnection(sendMsgOnConnect, conn.ID);
                }

                NetClientProc(conn);
            }
            catch (System.ObjectDisposedException)
            {
                // do nothing
            }
            catch (System.Exception ex)
            {
                //Debug.LogError("Connection to server failed: " + ex.Message);
                LogErrorToConsole("Connection to " + streamDesc + " server failed: " + ex.Message);
                //Debug.LogException(ex);
            }
        }

        private void NetClientProc(NetConnData conn)
        {
            conn.isActive = true;
            int bytesReceived = 0;
            byte[] bufferTmp = new byte[tmpBufferSize];

            try
            {
                while (conn.client.Connected && conn.isActive)
                {
                    if (conn.readyToSend && conn.stream.CanWrite && conn.messageQueue.Count > 0)
                    {
                        byte[] sendBuffer;
                        lock (conn.messageQueue)
                        {
                            sendBuffer = conn.messageQueue.Dequeue().WrapMessage();
                            conn.readyToSend = false;
                        }

                        conn.stream.BeginWrite(sendBuffer, 0, sendBuffer.Length, new System.AsyncCallback(MessageSentToServer), conn);
                    }

                    bytesReceived = 0;
                    while (conn.stream.DataAvailable)
                    {
                        conn.messageReceiveTime = (ulong)System.DateTime.Now.Ticks;
                        bytesReceived = conn.stream.Read(bufferTmp, 0, bufferTmp.Length);
                        ProcessReceivedData(bufferTmp, bytesReceived, conn);
                    }

                    Thread.Sleep(0);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }

            conn.isActive = false;
            conn.messageQueue.Clear();

            //Debug.Log("Connection " + conn.ID + " to " + conn.remoteEP + " closed.");
            LogToConsole("Connection to " + streamDesc + " server closed.");

            conn.stream.Close();
            conn.client.Close();

            if (connections.Contains(conn))
            {
                connections.Remove(conn);
            }
        }

        private void MessageSentToServer(System.IAsyncResult ar)
        {
            var conn = (NetConnData)ar.AsyncState;

            try
            {
                conn.stream.EndWrite(ar);
            }
            catch (System.Exception ex)
            {
                if(conn.isActive)
                {
                    //Debug.LogError("Exception occured while sending message to server.");
                    LogErrorToConsole("Error sending to " + streamDesc + " server: " + ex.Message);
                    //Debug.LogException(ex);

                    conn.isActive = false;
                }
            }

            conn.readyToSend = true;
        }

        private void ProcessReceivedData(byte[] buffer, int bytesReceived, NetConnData conn)
        {
            if (conn.bytesReceived == 0 && bytesReceived > 3)  //new message
            {
                int newMessageSize = System.BitConverter.ToInt32(buffer, 0) + sizeof(int);  //prefix is one int

                if (newMessageSize != conn.messageSize)
                {
                    conn.messageSize = newMessageSize;
                    conn.messageBuffer = new byte[newMessageSize];
                }
            }

            int availableLength = conn.messageSize - conn.bytesReceived;
            int copyLen = Mathf.Min(availableLength, bytesReceived);
            conn.packetCounter++;

            System.Array.Copy(buffer, 0, conn.messageBuffer, conn.bytesReceived, copyLen);
            conn.bytesReceived += copyLen;

            if (conn.bytesReceived == conn.messageSize)
            {
                conn.ResetCounters();

                NetMessageData message = new NetMessageData();
                message.SetDecompressor(decompressor);
                message.UnwrapMessage(conn.messageBuffer);

                ReceivedMessage?.Invoke(conn, new ReceivedMessageEventArgs(message));
            }

            if (copyLen != bytesReceived) // process the remainder of the message
            {
                byte[] newBuffer = new byte[bytesReceived - copyLen];
                System.Array.Copy(buffer, copyLen, newBuffer, 0, bytesReceived - copyLen);
                ProcessReceivedData(newBuffer, bytesReceived - copyLen, conn);
            }
        }

        // logs message to the console
        private void LogToConsole(string sMessage)
        {
            Debug.Log(sMessage);

            lock (sbConsole)
            {
                sbConsole.Clear();
                sbConsole.Append(sMessage); //.AppendLine();
            }
        }

        // logs error message to the console
        private void LogErrorToConsole(string sMessage)
        {
            Debug.LogError(sMessage);

            lock (sbConsole)
            {
                sbConsole.Clear();
                sbConsole.Append(sMessage); //.AppendLine();
            }
        }


        // logs error message to the console
        private void LogErrorToConsole(System.Exception ex)
        {
            LogErrorToConsole(ex.Message + "\n" + ex.StackTrace);
        }

    }

}

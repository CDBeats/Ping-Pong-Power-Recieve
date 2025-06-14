using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class BLEManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Button bluetoothButton;
    public Text paddleStatusText;

    [Header("BLE Settings")]
    public string primaryDeviceName = "Paddle";
    public string alternateDeviceName = "Arduino";
    public string serviceUUID = "e7f94bb9-9b07-5db7-8fbb-6b1cdbb5399e";
    public string charUUID = "12340000-0000-0000-0000-000000000000";
    public float scanTimeoutSeconds = 30f;
    public int maxScanRetries = 2;
    public float packetTimeoutSeconds = 5f;
    public float connectedMessageDuration = 5f;

    public event Action<string, Vector3, Vector3> OnImuDataReceived;

    private bool isScanningDevices = false;
    private bool isScanningServices = false;
    private bool isScanningCharacteristics = false;
    private bool isSubscribed = false;
    private string lastError;
    private float _lastPacketTime;
    private HashSet<string> _scannedDeviceNames = new HashSet<string>();
    private float lastLogTime;
    private int packetCount;

    private PaddleState paddle;

    public class PaddleState
    {
        public string playerId = "Player 1";
        public string charUUID;
        public string deviceId;
        public string serviceId;
        public Vector3 accelerometer = Vector3.zero;
        public Vector3 gyroscope = Vector3.zero;
        public bool imuValid = false;
        public float timeoutAt;
        public int scanRetries = 0;
        public string deviceName = "";
    }

    void Start()
    {
        BleApi.Quit();
        paddle = new PaddleState
        {
            charUUID = NormalizeUuid(charUUID)
        };
        if (bluetoothButton != null)
            bluetoothButton.onClick.AddListener(() => StartScan(true));
        lastError = "Ok";
        UpdateStatus("Ready to connect");
        StartScan(true);
        StartCoroutine(PollDataCoroutine());
    }

    void Update()
    {
        if (isScanningDevices)
        {
            if (Time.time > paddle.timeoutAt)
            {
                if (paddle.scanRetries < maxScanRetries)
                {
                    paddle.scanRetries++;
                    isScanningDevices = false;
                    BleApi.StopDeviceScan();
                    UpdateStatus($"Retry {paddle.scanRetries}/{maxScanRetries}...");
                    StartScan(false);
                    return;
                }
                ResetScan("Paddle not found. Try again.");
                return;
            }

            BleApi.DeviceUpdate du = new BleApi.DeviceUpdate();
            BleApi.ScanStatus status;
            do
            {
                status = BleApi.PollDevice(ref du, false);
                if (status == BleApi.ScanStatus.AVAILABLE)
                {
                    if (!string.IsNullOrEmpty(du.name) && !_scannedDeviceNames.Contains(du.name))
                    {
                        _scannedDeviceNames.Add(du.name);
                        Debug.Log($"Found device: {du.name}");
                    }

                    if (!string.IsNullOrEmpty(du.name) &&
                        (du.name == primaryDeviceName || du.name == alternateDeviceName))
                    {
                        isScanningDevices = false;
                        BleApi.StopDeviceScan();
                        paddle.deviceId = du.id;
                        paddle.deviceName = du.name;
                        UpdateStatus($"Found {du.name}, searching for service…");
                        StartServiceScan();
                        return;
                    }
                }
            } while (status == BleApi.ScanStatus.AVAILABLE);
        }

        if (isScanningServices)
        {
            if (Time.time > paddle.timeoutAt)
            {
                ResetScan("Service scan timeout");
                return;
            }

            BleApi.Service svc = new BleApi.Service();
            BleApi.ScanStatus status;
            do
            {
                status = BleApi.PollService(out svc, false);
                if (status == BleApi.ScanStatus.AVAILABLE)
                {
                    Debug.Log($"Found service: {svc.uuid}");
                    string normSvc = NormalizeUuid(svc.uuid);
                    string normTarget = NormalizeUuid(serviceUUID);
                    if (normSvc == normTarget)
                    {
                        isScanningServices = false;
                        paddle.serviceId = svc.uuid;
                        UpdateStatus("Found service, searching for characteristic…");
                        StartCharacteristicScan();
                        return;
                    }
                }
            } while (status == BleApi.ScanStatus.AVAILABLE);

            if (status == BleApi.ScanStatus.FINISHED && paddle.serviceId == null)
            {
                ResetScan("Service not found on device");
                return;
            }
        }

        if (isScanningCharacteristics)
        {
            if (Time.time > paddle.timeoutAt)
            {
                ResetScan("Characteristic scan timeout");
                return;
            }

            BleApi.Characteristic chr = new BleApi.Characteristic();
            BleApi.ScanStatus status;
            do
            {
                status = BleApi.PollCharacteristic(out chr, false);
                if (status == BleApi.ScanStatus.AVAILABLE)
                {
                    Debug.Log($"Found characteristic: {chr.uuid}");
                    string normChr = NormalizeUuid(chr.uuid);
                    if (normChr == paddle.charUUID)
                    {
                        isScanningCharacteristics = false;
                        bool subscribed = BleApi.SubscribeCharacteristic(paddle.deviceId, paddle.serviceId, chr.uuid, false);
                        BleApi.ErrorMessage em;
                        BleApi.GetError(out em);
                        string err = em.msg?.Trim().ToLower();
                        if (subscribed || err == "ok" || string.IsNullOrEmpty(err))
                        {
                            UpdateStatus("Waiting for data…");
                        }
                        else
                        {
                            UpdateStatus($"Subscription failed: {em.msg}");
                        }
                        return;
                    }
                }
            } while (status == BleApi.ScanStatus.AVAILABLE);

            if (status == BleApi.ScanStatus.FINISHED)
            {
                ResetScan("Characteristic not found on device");
                return;
            }
        }

        if (isSubscribed && Time.time - _lastPacketTime > packetTimeoutSeconds)
        {
            Debug.Log("Data timeout, restarting BLE scan...");
            StartScan(true);
            return;
        }

        BleApi.ErrorMessage emCheck;
        BleApi.GetError(out emCheck);
        if (!string.IsNullOrEmpty(emCheck.msg) && emCheck.msg != "Ok" && emCheck.msg != lastError)
        {
            Debug.Log($"[BLE] Error: {emCheck.msg}");
            lastError = emCheck.msg;
            UpdateStatus($"Error: {emCheck.msg}");
        }
    }

    IEnumerator PollDataCoroutine()
    {
        float pollInterval = 0.02f; // 50 Hz
        while (true)
        {
            BleApi.BLEData res = new BleApi.BLEData();
            while (BleApi.PollData(out res, false))
            {
                if (res.deviceId == paddle.deviceId)
                {
                    PumpData(res);
                }
            }
            yield return new WaitForSeconds(pollInterval);
        }
    }

    public void StartScan(bool resetRetryCount = true)
    {
        if (isScanningDevices) return;

        BleApi.Quit();
        ResetState();

        if (resetRetryCount)
            paddle.scanRetries = 0;

        _scannedDeviceNames.Clear();
        isScanningDevices = true;
        paddle.timeoutAt = Time.time + scanTimeoutSeconds;
        BleApi.StartDeviceScan();
        UpdateStatus("Scanning for paddle…");
    }

    void StartServiceScan()
    {
        if (isScanningServices) return;
        isScanningServices = true;
        paddle.timeoutAt = Time.time + scanTimeoutSeconds;
        BleApi.ScanServices(paddle.deviceId);
    }

    void StartCharacteristicScan()
    {
        if (isScanningCharacteristics) return;
        isScanningCharacteristics = true;
        paddle.timeoutAt = Time.time + scanTimeoutSeconds;
        BleApi.ScanCharacteristics(paddle.deviceId, paddle.serviceId);
    }

    void ResetScan(string message)
    {
        BleApi.Quit();
        isScanningDevices = false;
        isScanningServices = false;
        isScanningCharacteristics = false;
        ResetState();
        paddle.scanRetries = 0;
        UpdateStatus(message);
    }

    void ResetState()
    {
        BleApi.BLEData dummy;
        while (BleApi.PollData(out dummy, false)) { }

        paddle.deviceId = null;
        paddle.serviceId = null;
        paddle.deviceName = "";
        paddle.accelerometer = Vector3.zero;
        paddle.gyroscope = Vector3.zero;
        paddle.imuValid = false;
        isSubscribed = false;
    }

    void PumpData(BleApi.BLEData d)
    {
        if (d.size != 13) return;
        try
        {
            byte[] buf = d.buf;
            paddle.accelerometer = new Vector3(
                BitConverter.ToInt16(buf, 1) / 1000f,
                BitConverter.ToInt16(buf, 3) / 1000f,
                BitConverter.ToInt16(buf, 5) / 1000f
            );
            paddle.gyroscope = new Vector3(
                BitConverter.ToInt16(buf, 7) / 10f,
                BitConverter.ToInt16(buf, 9) / 10f,
                BitConverter.ToInt16(buf, 11) / 10f
            );
            paddle.imuValid = true;
            _lastPacketTime = Time.time;

            packetCount++;
            if (Time.time - lastLogTime >= 1f)
            {
                Debug.Log($"IMU Data Rate: {packetCount} Hz");
                packetCount = 0;
                lastLogTime = Time.time;
            }

            if (!isSubscribed)
            {
                isSubscribed = true;
                lastError = "Ok";
                UpdateStatus("Connected! Receiving data...");
                StartCoroutine(ClearConnectedMessage());
            }
            OnImuDataReceived?.Invoke(paddle.playerId, paddle.accelerometer, paddle.gyroscope);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BLE] Data parse error: {ex}");
            paddle.imuValid = false;
        }
    }

    private IEnumerator ClearConnectedMessage()
    {
        yield return new WaitForSeconds(connectedMessageDuration);
        UpdateStatus("");
    }

    void UpdateStatus(string msg)
    {
        if (paddleStatusText != null)
            paddleStatusText.text = msg;
    }

    string NormalizeUuid(string u)
    {
        return string.IsNullOrEmpty(u) ? "" :
            u.ToLowerInvariant().Replace("-", "").Replace("{", "").Replace("}", "").Trim('\0');
    }

    void OnApplicationQuit()
    {
        BleApi.Quit();
    }
}
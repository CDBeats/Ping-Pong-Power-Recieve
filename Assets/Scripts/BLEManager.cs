using UnityEngine;
using UnityEngine.UI;
using System;

public class BLEManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Button bluetoothButton;
    public Text paddleStatusText;

    [Header("BLE Settings")]
    public string expectedDeviceName = "Paddle 1";
    public string serviceUUID = "e7f94bb9-9b07-5db7-8fbb-6b1cdbb5399e";
    public string charUUID = "12340000-0000-0000-0000-000000000000";
    public float scanTimeoutSeconds = 30f;
    public int maxScanRetries = 2;

    public event Action<string, Vector3, Vector3> OnImuDataReceived;

    private bool isScanningDevices = false;
    private bool isScanningServices = false;
    private bool isScanningCharacteristics = false;
    private bool isSubscribed = false;
    private string lastError;

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
        BleApi.Quit(); // Ensures any previous BLE sessions are cleared.

        paddle = new PaddleState
        {
            charUUID = NormalizeUuid(charUUID)
        };

        if (bluetoothButton != null)
            bluetoothButton.onClick.AddListener(() => StartScan());

        lastError = "Ok";
        UpdateStatus("Ready to connect");

        StartScan();
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
                    StartScan();
                    return;
                }
                ResetScan("Timeout after retries, try again");
                return;
            }

            BleApi.DeviceUpdate du = new BleApi.DeviceUpdate();
            BleApi.ScanStatus status;
            do
            {
                status = BleApi.PollDevice(ref du, false);
                if (status == BleApi.ScanStatus.AVAILABLE)
                {
                    if (!string.IsNullOrEmpty(du.name) && du.name.Contains(expectedDeviceName))
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
                    string normChr = NormalizeUuid(chr.uuid);
                    if (normChr == paddle.charUUID)
                    {
                        // Optional: Uncomment if your plugin supports chr.properties
                        // if (!CharacteristicCanNotify(chr)) continue;

                        isScanningCharacteristics = false;

                        bool subscribed = BleApi.SubscribeCharacteristic(paddle.deviceId, paddle.serviceId, chr.uuid, false);
                        BleApi.ErrorMessage em;
                        BleApi.GetError(out em);

                        // Some APIs return "ok" even when subscription fails
                        string err = em.msg?.Trim().ToLower();
                        if (subscribed || err == "ok" || string.IsNullOrEmpty(err))
                        {
                            // Don't set subscribed yet — wait for actual data to confirm in PumpData
                            UpdateStatus("Waiting for data…");
                        }
                        else
                        {
                            UpdateStatus($"Sub failed: {em.msg}");
                        }

                        return;
                    }
                }
            } while (status == BleApi.ScanStatus.AVAILABLE);
        }

        BleApi.BLEData res = new BleApi.BLEData();
        while (BleApi.PollData(out res, false))
        {
            if (res.deviceId == paddle.deviceId)
            {
                PumpData(res);
            }
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

    void StartScan()
    {
        if (isScanningDevices) return;

        ResetState();
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
        isScanningDevices = false;
        isScanningServices = false;
        isScanningCharacteristics = false;
        BleApi.StopDeviceScan();
        ResetState();
        UpdateStatus(message);
    }

    void ResetState()
    {
        paddle.deviceId = null;
        paddle.serviceId = null;
        paddle.deviceName = "";
        paddle.accelerometer = paddle.gyroscope = Vector3.zero;
        paddle.imuValid = false;
        paddle.scanRetries = 0;
        isSubscribed = false;
    }

    void PumpData(BleApi.BLEData d)
    {
        if (d.size != 13) return;

        try
        {
            paddle.accelerometer = new Vector3(
                BitConverter.ToInt16(d.buf, 1) / 1000f,
                BitConverter.ToInt16(d.buf, 3) / 1000f,
                BitConverter.ToInt16(d.buf, 5) / 1000f
            );
            paddle.gyroscope = new Vector3(
                BitConverter.ToInt16(d.buf, 7) / 10f,
                BitConverter.ToInt16(d.buf, 9) / 10f,
                BitConverter.ToInt16(d.buf, 11) / 10f
            );
            paddle.imuValid = true;

            if (!isSubscribed)
            {
                isSubscribed = true;
                UpdateStatus("Connected! Receiving data...");
            }

            OnImuDataReceived?.Invoke(paddle.playerId, paddle.accelerometer, paddle.gyroscope);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BLE] Data parse error: {ex}");
            paddle.imuValid = false;
        }
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

    // Optional: Uncomment if your BLE plugin provides `chr.properties`
    /*
    private bool CharacteristicCanNotify(BleApi.Characteristic chr)
    {
        return (chr.properties & 0x30) != 0; // 0x10 notify | 0x20 indicate
    }
    */

    void OnApplicationQuit()
    {
        BleApi.Quit();
    }
}

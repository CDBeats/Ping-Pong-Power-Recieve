#include <Arduino_BMI270_BMM150.h>
#include <ArduinoBLE.h>

// BLE Configuration
BLEService imuService("e7f94bb9-9b07-5db7-8fbb-6b1cdbb5399e");
BLECharacteristic imuDataChar(
  "12340000-0000-0000-0000-000000000000",
  BLERead | BLENotify,
  13  // 1 byte packet counter + 6 * 2-byte values = 13 bytes
);

unsigned long lastUpdateTime = 0;
const unsigned long UPDATE_INTERVAL = 20;  // 50 Hz
uint8_t packetCounter = 0;

// Utility to set RGB LED: pass true to turn that color on, false to turn it off.
// On Nano 33 BLE Sense Rev2, LEDR, LEDG, LEDB are active LOW: LOW turns LED on.
void setLEDColor(bool redOn, bool greenOn, bool blueOn) {
  digitalWrite(LEDR, redOn   ? LOW : HIGH);
  digitalWrite(LEDG, greenOn ? LOW : HIGH);
  digitalWrite(LEDB, blueOn  ? LOW : HIGH);
}

// BLE event handlers:
void bleConnected(BLEDevice central) {
  // Show Blue on connection
  setLEDColor(false, false, true);
}

void bleDisconnected(BLEDevice central) {
  // Show Red on disconnect, restart advertising
  setLEDColor(true, false, false);
  BLE.advertise();
}

void setup() {
  // Initialize onboard LED pins
  pinMode(LEDR, OUTPUT);
  pinMode(LEDG, OUTPUT);
  pinMode(LEDB, OUTPUT);
  // Show Red initially (not connected)
  setLEDColor(true, false, false);
  IMU.begin(); // Initialize IMU
  BLE.begin(); // Initialize BLE

  BLE.setLocalName("Paddle");
  BLE.setAdvertisedService(imuService);
  imuService.addCharacteristic(imuDataChar);
  BLE.addService(imuService);

  // Register event handlers before advertising
  BLE.setEventHandler(BLEConnected,    bleConnected);
  BLE.setEventHandler(BLEDisconnected, bleDisconnected);

  BLE.advertise();
}

void loop() {
  // Handle BLE events (connect/disconnect)
  BLE.poll();

  unsigned long now = millis();
  if (now - lastUpdateTime < UPDATE_INTERVAL) {
    return;
  }
  lastUpdateTime = now;

  // Only read/send if a central is connected
  if (!BLE.connected()) {
    return;
  }

  float ax, ay, az;
  float gx, gy, gz;
  // Read raw acceleration and gyroscope; returns true if both succeed
  bool ok = IMU.readAcceleration(ax, ay, az) && IMU.readGyroscope(gx, gy, gz);
  if (!ok) {
    // Skip sending this cycle
    return;
  }

  // Prepare a 13-byte packet:
  // [0]: packet counter
  // [1..2]: accel X scaled (int16)
  // [3..4]: accel Y
  // [5..6]: accel Z
  // [7..8]: gyro X scaled
  // [9..10]: gyro Y
  // [11..12]: gyro Z
  uint8_t packet[13];
  packet[0] = packetCounter++;

  int16_t* dataPtr = (int16_t*)(packet + 1);
  // Scale acceleration in g -> milli-g:
  dataPtr[0] = (int16_t)(ax * 1000); 
  dataPtr[1] = (int16_t)(ay * 1000);
  dataPtr[2] = (int16_t)(az * 1000);
  // Scale gyro in deg/s -> tenths deg/s:
  // Scale gyro in deg/s -> tenths deg/s
  // Mounting Corrections :
  dataPtr[3] = (int16_t)(gx * 10);  // Inverted pitch
  dataPtr[4] = (int16_t)(gy * 10);  // Inverted Yaw 
  dataPtr[5] = (int16_t)(gz * 10);  // Yaw and Roll swapped

  // Write the characteristic (will notify if subscribed)
  imuDataChar.writeValue(packet, sizeof(packet));
}

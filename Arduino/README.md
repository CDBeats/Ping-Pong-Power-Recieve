#### 📥 **Installing Firmware to the Arduino Nano 33 BLE Rev2**

Follow these steps to upload the IMU firmware to your Arduino Nano 33 BLE Rev2.



###### ⚙️ **Setup**

1. Install the Arduino IDE
   ● Download from: https://www.arduino.cc/en/software
2. Open the Sketch
   ● Go to the Arduino/IMU\_over\_BLE folder in this project.
   ● Open the .ino file in Arduino IDE.
3. Install the Required Board Package
   ● In Arduino IDE, open Boards Manager (left sidebar → “Boards” icon)
   ● Search for Arduino Mbed OS Nano Boards
   ● Click Install
4. Install the Required Libraries
   ● Open Library Manager (left sidebar → “Libraries” icon)
   ● Search and install:
   ○ Arduino\_BMI270\_BMI160
   ○ ArduinoBLE



###### 🔌 **Upload the Firmware**

1. Connect the Arduino
   ● Plug your Arduino Nano 33 BLE Rev2 into your computer’s USB port
2. Select the Board
   ● In the top menu bar, choose the board dropdown
   ● Select: Arduino Nano 33 BLE (COM\_x)
3. Upload
   ● Click the Upload (→) button at the top-left of the IDE
   ● Keep the board plugged in until upload completes successfully



✅ Your Arduino is now flashed with the IMU-over-BLE firmware.


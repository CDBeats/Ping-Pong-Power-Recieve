Ping Pong Power Receive

Ping Pong Power Receive is a Unity-based motion-controlled ping pong game.
It uses an Arduino Nano 33 BLE Rev2 inside a custom-built paddle to stream IMU motion data over Bluetooth Low Energy (BLE) to the game, allowing players to hit virtual ping pong balls by swinging the physical paddle.

Demo
(Add a gameplay GIF or video here)
(Add photos of the paddle hardware if you want)

Requirements
To play or develop this project, you will need:
Unity (insert tested version number)
Arduino Nano 33 BLE Rev2
BLE-compatible computer (Windows/macOS)
Bluetooth 4.0+ adapter
USB cable for programming the Arduino
Optional: 3D printed paddle housing and battery pack

How to Run
Download or clone this repository.
Power on the paddle (Arduino Nano 33 BLE Rev2).
Launch the game executable from the Build folder.
Wait for the game to automatically connect to the paddle over BLE.
Start playing.

How to Build on Unity
Install Unity (insert version number).
Open the Unity Project folder from this repository in Unity Hub.
Press Play in the Unity Editor to run the game.

To build a standalone executable:
Go to File > Build Settings
Select your platform (Windows/macOS)
Click Build

Hardware Assembly
To build the motion-sensing paddle, refer to the assembly guide in:
/paddle/README.md
This includes 3D printing, wiring, and circuit instructions.

BLE Communication Details
The Arduino Nano 33 BLE Rev2 streams IMU data (accelerometer + gyroscope).
Update interval: (insert ms value)
Data format: ax,ay,az,gx,gy,gz
The Unity game reads and processes this data to control the paddle's rotation in-game.

Troubleshooting
Paddle won't connect:
Make sure the Arduino is powered on and broadcasting before starting the game.
Check Bluetooth permissions on your computer.

Game is laggy:
Close background applications
Lower graphics settings in Unity build

TP5100 LEDs:
Red = Charging
Blue = Fully charged

License
This project is licensed under the MIT License.
See LICENSE for details.

Contributing
Pull requests are welcome.

If you'd like to contribute:
Fork the repository
Create a new branch
Submit a pull request describing your changes

Credits
Created by Christian Daniel
Developed as a Grade 11 Physics final project

Built using:
Unity Game Engine
Arduino Nano 33 BLE Rev2
BLE communication via BleWinrtDLL API

Known Issues / Future Plans
IMU data tied to Unity FPS:
Currently, IMU readings are processed directly in Unity’s update loop. This can cause problems on lower-end machines, including input lag and potential data overflow. Future versions should decouple IMU data handling from the frame rate to ensure consistent paddle response regardless of FPS.

Translational movement issues:
While rotational movement tracks accurately, lateral (side-to-side) and forward/backward motion is unreliable. Alternative methods for translational tracking will be explored, such as velocity-based integration, predictive smoothing, or sensor fusion techniques.

Consider 9DOF IMU:
The current 6DOF setup (accelerometer + gyroscope) handles rotation well but lacks a magnetometer. Adding a 9DOF IMU could improve translational tracking and overall positional accuracy, though it would require a new Arduino-compatible IMU.

Power / Charging improvements:
Currently, the paddle is powered through VIN, which requires a higher working voltage (≈7–8.4V). This can make the TP5100 warm when charging while in use. A future improvement would be to use a buck converter to step the battery voltage down to 5V and power the Arduino through the 5V pin instead of VIN. Benefits include safe simultaneous charging and operation, reduced heat, and stable voltage to the Arduino even as the batteries discharge.

Visual / gameplay polish:
Game physics and visuals are currently functional but minimal. Future updates could improve graphics, add scoring, sound effects, and provide better feedback for the player.

General improvements:
Better wire management in the paddle housing
More durable switch and DC jack mountings
Optional battery level indicators or alerts
When I figure out how to, I will make a pcb instead of freely soldering wires.

#### 🧩 **Power Paddle Assembly Instructions**

Follow these steps to build the circuit / 3D printed assembly of the Paddle.



###### ⚙️ **Tools / prerequisites you must have:**

● Accessible 3d printer

● Ability to solder



###### 📦 **Necessary Parts**

**Prints**

● v3\_Handle.stl - The handle print as well as an embellishment.

● v3\_Paddle.stl	- The main paddle print.



**Products**

● Arduino Nano 33 BLE rev2	- *https://a.co/d/asJpT6n*

● TP5100 Charging Module	- *https://a.co/d/fDPULpB*

● 18650 li-ion batteries 	- *https://a.co/d/hdkdgDc*

● 18650 battery holders 	- *https://a.co/d/c8JzIbU*

● 22AWG Wire Spool		- *https://a.co/d/eIrYgt8*

● 12V 1A AC/DC Power Supply	- *https://a.co/d/6qQUMe8*

● 5.5mm x 2.1mm DC Jack 	- *https://a.co/d/4fNy4mj*

● Slide Switch			- *https://a.co/d/2ssjUbd*

● Heat shrink 			- *https://a.co/d/1eW2gUw*

● Any plastic-holding glue 	- *https://a.co/d/diy9VXv*

● Hot glue



The print and circuit assembly are done separately.

*I recommended watching the instructional video rather than following these instructions.*



###### 🖨 **Print Assembly**

1. Ensure that the side of the paddle with the alcove (door for the Arduino) is facing up.
2. Ensure that the side of the handle with the alcove (door for the batteries) is facing up.
3. Apply readied epoxy to the inside of the handle print where it will meet with the paddle.
4. Slide the paddle into place. The printed-in embellishment on the upward-facing side of the paddle will guide the handle into place.
5. Let rest until epoxy has dried.

⚠️ Ensure both parts are fully aligned before the epoxy sets — you won’t be able to adjust it after it cures.



###### ⚡ **Circuit Assembly**

*PLEASE WATCH THE VIDEO FOR YOUR BENEFIT. THIS PART IS COMPLICATED. VERY HACKY BUILD.*

**You are creating a 2S battery pack — two 18650 cells in series (≈ 7.4V nominal and 8.4V fully charged).**

1. Establish a wire connecting the two battery holders from the negative (-) wire of one holder to the positive (+) wire of the other. This puts them in series.
2. Glue the two battery holders together (ensuring the hole on both battery holders is clear of glue) so that they extend in length rather than width.
3. Route a +/- wire pair through the center hole between the holders.
4. Solder the negative wire to any metal on the negative terminal of the battery holder (the spring side) or to the existing black wire (if it’s thick enough).
5. Extend the positive wire further out through the hole beside the spring.
6. Solder another black wire to the same spring, and route it through the same hole as the red one.
7. Solder the DC Jack terminals to these two wires.
   ● Red goes to the short pin (center positive)
   ● Black goes to the long pin (outer sleeve negative)
   ● This creates your DC input connection.
8. At the other end of the battery holders, make small holes at the very end on both sides of the holder. The same wire routed to the DC jack will go through the hole on its side to the VIN+ and VIN- pins on the TP5100.
9. Use hot glue to stick a TP5100 chip to the outside of the holders, on the positive end (no spring). Ensure that VIN+ and VIN- are facing the hole where you routed the DC jack wires.
10. Solder the black DC wire to VIN- and the red DC wire to VIN+.
11. On the TP5100, also solder a short, thick wire from VIN- to B-.
    ● ⚠️ THIS IS CRUCIAL for the TP5100 to function correctly.\\
12. On the TP5100, bridge (solder together) the “SET” pads to enable 2-cell mode.
13. In the battery holder, grab the already-present red wire and solder that to B+ on the TP5100.
14. Now, get another +/- pair of wires.
    ● Solder the black wire to B-
    ● Solder the red wire to B+
    ● These will power the Arduino.
15. Add the slide switch on the red wire going to the Arduino.
    ● The red wire from B+ goes to one pin of the switch.
    ● A second red wire from the other pin on the switch goes to the Arduino VIN.
    ● The black wire from B- goes straight to Arduino GND.
    ● ⚡ If your switch has 3 pins, use only the middle (common) and one side pin.
16. Insert one battery, then the other. Slide the switch on.
    ● If the Arduino turns on... you’ve done it 🎉
17. If it hasn’t turned on, check over the circuit diagram and the video to ensure you followed every step.



###### 🔧 **Final Assembly**

*With the 3D printed parts fully cured and the circuit tested and working, it’s time to assemble everything into the enclosure.*



⚠️ Important: Before starting, remove the batteries from the battery holders. This prevents accidental shorts or damage during assembly.



1. Insert the Arduino
   ● Slide the Arduino Nano 33 BLE Rev2 through the access hole in the handle toward the paddle side.
   ● Ensure the Arduino is upright and fully seated.
2. Install the Slide Switch
   ● Gently press-fit the slide switch into its printed housing on the handle.
   ● Make sure the switch is secure and aligned for easy toggling from the outside.
3. Mount the DC Jack
   ● Feed the DC jack (already wired to the TP5100 and battery pack) into its printed opening from inside the handle.
   ● Press it firmly into place until it’s flush and snug.
4. Install the Battery Holders
   ● Carefully slide the battery holders into their printed housing inside the handle.
   ● Ensure the wires are not pinched and that the holders sit flat and secure.
5. Arrange Wiring
   ● Carefully tuck excess wires inside the handle cavity so they do not block the battery doors or press against the Arduino.
   ● Use small dots of hot glue if needed to keep wires from shifting around.
6. Insert the Batteries
   ● Once everything is secured, slide both 18650 batteries into their holders, ensuring the correct polarity.

###### 

###### ⚠️ **Final Notes**

● Do not insert batteries or plug in power until all solder joints are complete and you’ve double-checked for shorts.

● The TP5100 can get warm during charging. This is normal, but make sure it has some airflow.

● The Arduino Nano 33 BLE Rev2 draws very little current, so it’s safe to run while charging the batteries.

● This design does not have automatic power path management, so be aware that the TP5100 will still try to charge even while the Arduino is on — which is okay for this build, just not perfect.


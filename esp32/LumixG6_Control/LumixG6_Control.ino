#include <Arduino.h>

const int PIN_FOCUS   = D7; 
const int PIN_SHUTTER = D9;

void setup() {
    Serial.begin(115200);
    pinMode(PIN_FOCUS, OUTPUT);
    pinMode(PIN_SHUTTER, OUTPUT);
    
    digitalWrite(PIN_FOCUS, LOW);
    digitalWrite(PIN_SHUTTER, LOW);
}

void loop() {
    if (Serial.available() > 0) {
        char command = Serial.read();

        // COMMAND 0: Off1
        if (command == '0') {
            digitalWrite(PIN_SHUTTER, LOW);
            digitalWrite(PIN_FOCUS, LOW);
            Serial.println("Off");
        }

        // COMMAND 1: Focus
        if (command == '1') {
            digitalWrite(PIN_FOCUS, HIGH);
            Serial.println("Focus");
        } 
        
        // COMMAND 2: Shutter
        else if (command == '2') {
            digitalWrite(PIN_SHUTTER, HIGH);
            Serial.println("Shutter");
        }
        
        // COMMAND 3: Single Short Pulse (For Biases using Manual Shutter Speed)
        else if (command == '3') {
            digitalWrite(PIN_FOCUS, HIGH);
            delay(100); 
            digitalWrite(PIN_SHUTTER, HIGH);
            delay(150); // Hardcoded mechanical pulse duration
            digitalWrite(PIN_SHUTTER, LOW);
            digitalWrite(PIN_FOCUS, LOW);
            Serial.println("Pulse");
        }
    }
    delay(1);
}
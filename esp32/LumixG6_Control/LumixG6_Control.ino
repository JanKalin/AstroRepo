#include <Arduino.h>

const int PIN_SHUTTER = D10;

void setup() {
    Serial.begin(115200);
    pinMode(PIN_SHUTTER, OUTPUT);
    digitalWrite(PIN_SHUTTER, LOW);
}

void loop() {
    if (Serial.available() > 0) {
        char command = Serial.read();

        // COMMAND 1: On
        if (command == '1') {
            digitalWrite(PIN_SHUTTER, HIGH);
        } 
        
        // COMMAND 0: Off1
        if (command == '0') {
            digitalWrite(PIN_SHUTTER, LOW);
        }
    }
    delay(1);
}
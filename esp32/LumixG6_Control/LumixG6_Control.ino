#include <Arduino.h>

// Pin Definitions for Seeed ESP32-C6
const int PIN_SHUTTER = D10;
const int PIN_LED     = 15; // Built-in green LED on the Seeed C6 board

void setup() {
    Serial.begin(115200);
    
    // Configure pins as outputs
    pinMode(PIN_SHUTTER, OUTPUT);
    pinMode(PIN_LED, OUTPUT);
    
    // Ensure everything is turned off at initialization
    digitalWrite(PIN_SHUTTER, LOW);
    digitalWrite(PIN_LED, HIGH);
}

void loop() {
    if (Serial.available() > 0) {
        char command = Serial.read();

        if (command == '1') {
            // NINA starts exposure: open optocoupler gate AND turn on status LED
            digitalWrite(PIN_SHUTTER, HIGH);
            digitalWrite(PIN_LED, LOW);
        } 
        else if (command == '0') {
            // NINA ends exposure: release optocoupler gate AND turn off status LED
            digitalWrite(PIN_SHUTTER, LOW);
            digitalWrite(PIN_LED, HIGH);
        }
    }
    // High-frequency loop pacing
    delay(1);
}
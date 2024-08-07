using System.Collections.Generic;
using System;
using System.Numerics;

namespace Spectrum {
  public class OrientationDevice {
    public int timestamp { get; set; }
    public Quaternion calibrationOrigin { get; set; }
    public Quaternion currentOrientation { get; set; }
    public int actionFlag { get; set; }

    // Buffer to store last 10 orientation states and their timestamps
    private Queue<Tuple<int, Quaternion>> orientationBuffer;

    public OrientationDevice(int timestamp, Quaternion calibrationOrigin, Quaternion currentOrientation) {
      this.timestamp = timestamp;
      this.calibrationOrigin = calibrationOrigin;
      this.currentOrientation = currentOrientation;
      actionFlag = 0;
      orientationBuffer = new Queue<Tuple<int, Quaternion>>(10);
      StoreOrientation(timestamp, currentOrientation);
    }

    public void calibrate() {
      calibrationOrigin = currentOrientation;
    }

    public Quaternion currentRotation() {
      return Quaternion.Multiply(Quaternion.Inverse(currentOrientation), calibrationOrigin);
    }

    public void StoreOrientation(int timestamp, Quaternion orientation) {
      // Store the orientation with timestamp
      if (orientationBuffer.Count == 10) {
        orientationBuffer.Dequeue(); // Remove the oldest entry if buffer is full
      }
      orientationBuffer.Enqueue(new Tuple<int, Quaternion>(timestamp, orientation));
    }

    public float ApproximateSpeed() {
      lock (orientationBuffer) {
        if (orientationBuffer == null || orientationBuffer.Count < 2) {
          return 0.0f; // Not enough data to calculate speed or buffer is not initialized
        }

        float totalAngleChange = 0.0f;
        float totalTime = 0.0f;
        // Convert the queue to an array
        // Ensure the operation is thread-safe
        Tuple<int, Quaternion>[] bufferCopy;
        try {
          bufferCopy = orientationBuffer.ToArray();
        }

        catch (ArgumentException ex) {
          // Handle the case where the array conversion fails
          // Return a default value or handle the exception accordingly
          return 0.0f;
        }


        for (int i = 1; i < bufferCopy.Length; i++) {
          var previous = bufferCopy[i - 1];
          var current = bufferCopy[i];

          // Check for null entries in bufferCopy
          if (previous == null || current == null) {
            continue; // Skip any null entries
          }

          int timeDiff = current.Item1 - previous.Item1;
          Quaternion deltaOrientation = Quaternion.Multiply(current.Item2, Quaternion.Inverse(previous.Item2));
          float angle = 2.0f * (float)Math.Acos(deltaOrientation.W);
          float angleDegrees = angle * (180.0f / (float)Math.PI);

          totalAngleChange += angleDegrees;
          totalTime += timeDiff / 1000.0f; // Convert to seconds
        }

        // Approximate speed as the total angular change divided by the total time
        return totalTime > 0 ? totalAngleChange / totalTime : 0.0f;
      }
    }
  }
}

using System.Collections.Generic;
using System;
using System.Numerics;

namespace Spectrum {
  public class OrientationDevice {
    public int timestamp { get; set; }
    public Quaternion calibrationOrigin { get; set; }
    public Quaternion currentOrientation { get; set; }
    public int actionFlag { get; set; }


    // Buffer to store last 10 distances and their timestamps
    private Queue<Tuple<int, float>> distanceBuffer; // Buffer to store distances and time differences
    private Quaternion lastOrientation; // Variable to store the last orientation
    private int lastTimestamp;


    public OrientationDevice(int timestamp, Quaternion calibrationOrigin, Quaternion currentOrientation) {
      this.timestamp = timestamp;
      this.calibrationOrigin = calibrationOrigin;
      this.currentOrientation = currentOrientation;
      actionFlag = 0;
      // Instantiate the buffer
      distanceBuffer = new Queue<Tuple<int, float>>(500);
      // Initialize last orientation
      lastOrientation = currentOrientation;
      lastTimestamp = timestamp;
      StoreOrientation(timestamp, currentOrientation);
    }

    public void calibrate() {
      calibrationOrigin = currentOrientation;
    }

    public Quaternion currentRotation() {
      return Quaternion.Multiply(Quaternion.Inverse(currentOrientation), calibrationOrigin);
    }

    public void StoreOrientation(int timestamp, Quaternion orientation) {
      lock (distanceBuffer) {
        // Calculate the distance moved on the sphere since the last orientation
        if (lastOrientation != null) {
          float distance = CalculateDistance(lastOrientation, orientation);
          int timeDiff = timestamp - this.timestamp;
          //Console.WriteLine(timeDiff);
          if (distanceBuffer.Count == 500) {
            distanceBuffer.Dequeue(); // Remove the oldest entry if buffer is full
          }
          distanceBuffer.Enqueue(new Tuple<int, float>(timeDiff, distance));
        }

        // Update last orientation and timestamp
        lastOrientation = orientation;
        this.timestamp = timestamp;
      }
    }

    private float CalculateDistance(Quaternion prevOrientation, Quaternion currOrientation) {
      // Ensure the shortest path is taken by checking the dot product
      float dot = Quaternion.Dot(prevOrientation, currOrientation);

      // If the dot product is negative, negate one quaternion to ensure the shortest arc is calculated
      if (dot < 0.0f) {
        currOrientation = new Quaternion(-currOrientation.X, -currOrientation.Y, -currOrientation.Z, -currOrientation.W);
        dot = -dot;
      }

      // Calculate the angle between the two orientations
      Quaternion deltaOrientation = Quaternion.Multiply(currOrientation, Quaternion.Inverse(prevOrientation));
      float angle = 2.0f * (float)Math.Acos(Math.Min(Math.Abs(deltaOrientation.W), 1.0f));

      return angle; // On a unit sphere, the angle is the arc length, which is the distance
    }
    public float SumDistances() {
      // Make a copy of the buffer to work with, ensuring thread safety
      if (distanceBuffer == null) {
        return 0.0f;
      }
        lock (distanceBuffer) {
        Tuple<int, float>[] bufferCopy = distanceBuffer.ToArray();

        float totalDistance = 0.0f;
        int distancesSummed = 0;

        // No need to lock bufferCopy because it is a local copy and thread-safe
        //Console.WriteLine();
        foreach (var entry in bufferCopy) {
          // Ensure division is safe, guard against divide by zero
          if (entry.Item1 != 0) {
            //Console.WriteLine(entry.Item2);
            totalDistance += (entry.Item2 * (float)entry.Item1) * 100;
            distancesSummed += 1;
          } else { 
          Console.WriteLine("zerofound");
        }
        }
        // Console.WriteLine()
       // return ((totalDistance));

        return (float)(int)((totalDistance / (float)distancesSummed));
      }
    }
  }
}

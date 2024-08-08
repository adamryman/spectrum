using Spectrum.Base;
using Spectrum.LEDs;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace Spectrum.Visualizers {
  class LEDDomeQuaternionMultiTestVisualizer : Visualizer {


    private Configuration config;
    private OrientationInput orientation;
    private LEDDomeOutput dome;
    private LEDDomeOutputBuffer buffer;

    private Vector3 spot = new Vector3(0, 1, 0);
    private readonly object mLock = new object();

    public LEDDomeQuaternionMultiTestVisualizer(
      Configuration config,
      OrientationInput orientation,
      LEDDomeOutput dome
    ) {
      this.config = config;
      this.orientation = orientation;
      this.dome = dome;
      dome.RegisterVisualizer(this);
      buffer = dome.MakeDomeOutputBuffer();
    }

    public int Priority {
      get {
        return this.config.domeActiveVis == 5 ? 2 : 0;
      }
    }

    public bool Enabled { get; set; }

    public Input[] GetInputs() {
      return new Input[] { this.orientation };
    }

    void Render() {

      // Global effects
      // Fade out
      buffer.Fade(1 - Math.Pow(5, -this.config.domeGlobalFadeSpeed), 0);

      // Store the device states as of this frame; this avoids problems when the devices get updated
      // in another thread
      Dictionary<int, OrientationDevice> devices;
      devices = new Dictionary<int, OrientationDevice>(orientation.devices);
      Dictionary<int, float> devicesSpeed;
      devicesSpeed = new Dictionary<int, float>();
      Dictionary<int, Scaler> devicesScale;
      devicesScale = new Dictionary<int, Scaler>();
      foreach (var key in devices.Keys) {
        devicesSpeed[key] = 0.0f;
        devicesScale[key] = new Scaler();
      }
      for (int i = 0; i < buffer.pixels.Length; i++) {
        var p = buffer.pixels[i];
        var x = 2 * p.x - 1; // now centered on (0, 0) and with range [-1, 1]
        var y = 1 - 2 * p.y; // this is because in the original mapping x, y come "out of" the top left corner
        float z = (x * x + y * y) > 1 ? 0 : (float)Math.Sqrt(1 - x * x - y * y);
        Vector3 pixelPoint = new Vector3((float)x, (float)y, z);

        // # Spotlight - orientation sensor dot
        // Calibration assigns (0, 1, 0) to be 'forward'
        // So we want the post-transformed pixel closest to (0, 1, 0)?
        double radius = .4;
        foreach (int deviceId in devices.Keys) {
          Quaternion currentOrientation = devices[deviceId].currentRotation();
          
          float currentSpeed = devices[deviceId].SumDistances();
          if (devicesSpeed[deviceId] != currentSpeed) {
            Console.WriteLine(currentSpeed);
            devicesSpeed[deviceId] = currentSpeed;
          }
          double distance = Vector3.Distance(Vector3.Transform(pixelPoint, currentOrientation), spot);
          int sat = 1;
          if (devices[deviceId].actionFlag == 1) {
            radius = .4;
            sat = 0;
          } else {
            radius = 1 - devicesScale[deviceId].Scale(currentSpeed);
            if ( radius < 0.1f ) {
              radius = 0.1f;
            }
            sat = 1;
          }
          if (distance < radius) {
            double L = (radius - distance) / radius;
            double hue = (double)Array.IndexOf(devices.Keys.ToArray(), deviceId) / devices.Count;
            Color color = new Color(hue, sat, 1);
            buffer.pixels[i].color = Color.BlendLightPaint(new Color(buffer.pixels[i].color), color).ToInt();
          }
        }
      }
      dome.WriteBuffer(buffer);
    }

    public void Visualize() {
      Render();
      dome.Flush();
    }
  }
  public class Scaler {
    private double _minValue = 0;
    private double _maxValue = 175;

    // Method to scale a value between 0 and 1
    public double Scale(double value) {
      // Update the min and max values seen so far
      //if (value < _minValue) _minValue = value;
      //if (value > _maxValue) _maxValue = value;

      // Handle the case where the min and max are the same
      if (_minValue == _maxValue) return 0.5;

      // Linearly scale the value between 0 and 1
      double scale = (1 - (value - _minValue) / (_maxValue - _minValue));
      if (scale > .9) {
        scale = .9;
      }
      if (scale < .2 ){
        scale = .2;
      }
      return (scale)*10;
    }

    // Optional: Method to reset the scaler
    public void Reset() {
      _minValue = double.MaxValue;
      _maxValue = double.MinValue;
    }

    // Optional: Method to get current min and max values
    public double MinValue => _minValue;
    public double MaxValue => _maxValue;
  }

}

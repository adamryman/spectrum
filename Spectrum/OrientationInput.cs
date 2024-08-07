using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using Spectrum.Base;
namespace Spectrum {

  public class OrientationInput : Input {
    private readonly Configuration config;
    public Dictionary<int, OrientationDevice> devices;
    private Dictionary<int, long> lastSeen;
    private long[] lastEvent;
    private Thread listenThread;
    private readonly object mLock = new object();
    private readonly static long DEVICE_TIMEOUT_MS = 5000;
    private readonly static long DEVICE_EVENT_TIMEOUT = 50;

    public OrientationInput(Configuration config) {
      this.config = config;
      devices = new Dictionary<int, OrientationDevice>();
      lastSeen = new Dictionary<int, long>();
      lastEvent = new long[255];
      listenThread = new Thread(new ThreadStart(Run));
      listenThread.Start();
    }
    public struct UdpState {
      public UdpClient u;
      public IPEndPoint e;
    }

    public bool Active {
      get {
        return true;
      }
      set { }
    }

    public bool AlwaysActive {
      get {
        return true;
      }
    }

    public bool Enabled {
      get {
        return true;
      }
    }

    private void ReceiveCallback(IAsyncResult ar) {
      UdpClient u = ((UdpState)(ar.AsyncState)).u;
      IPEndPoint e = ((UdpState)(ar.AsyncState)).e;
      UdpState s = new UdpState();
      s.e = e;
      s.u = u;
      byte[] buffer = u.EndReceive(ar, ref e);
      var deviceId = buffer[0];
      var timestamp = BitConverter.ToInt32(buffer, 1);

      // Device removal logic remains the same...

      // Datagram unpacking
      short W = BitConverter.ToInt16(buffer, 5);
      short X = BitConverter.ToInt16(buffer, 7);
      short Y = BitConverter.ToInt16(buffer, 9);
      short Z = BitConverter.ToInt16(buffer, 11);
      var actionFlag = buffer[13];
      Quaternion sensorState = new Quaternion(X / 16384.0f, Y / 16384.0f, Z / 16384.0f, W / 16384.0f);

      // Device state update
      lock (mLock) {
        if (!devices.ContainsKey(deviceId)) {
          devices.Add(deviceId, new OrientationDevice(timestamp, new Quaternion(0, 0, 0, 1), sensorState));
        } else {
          var device = devices[deviceId];
          if (actionFlag != 0) {
            // Debounce logic remains the same...
          }
          if (timestamp > device.timestamp || timestamp < (device.timestamp - 1000)) {
            device.timestamp = timestamp;
            device.currentOrientation = sensorState;
            device.StoreOrientation(timestamp, sensorState); // Store the new orientation in the buffer
          }
        }
      }

      u.BeginReceive(new AsyncCallback(ReceiveCallback), s);
    }
    private void Run() {
      IPEndPoint e = new IPEndPoint(IPAddress.Any, 5005);
      UdpClient u = new UdpClient(e);

      UdpState s = new UdpState();
      s.e = e;
      s.u = u;
      u.BeginReceive(new AsyncCallback(ReceiveCallback), s);
    }
    public void OperatorUpdate() {
      if (config.orientationCalibrate) {
        // This is when the "Calibrate" button in the UI window is hit
        // Calibrates all devices at once
        // Calibration target is 'forwards' in the y-direction
        lock(mLock) {
          foreach (int id in devices.Keys) {
            devices[id].calibrate();
          }
        }
        config.orientationCalibrate = false;
      }
    }

    public Quaternion deviceRotation(int deviceId) {
      lock (mLock) {
        if (devices.ContainsKey(deviceId)) {
          return devices[deviceId].currentRotation();
        } else {
          return new Quaternion(0, 0, 0, 1);
        }
      }
    }

    public Quaternion deviceCalibration(int deviceId) {
      lock (mLock) {
        if (devices.ContainsKey(deviceId)) {
          return devices[deviceId].calibrationOrigin;
        } else {
          return new Quaternion(0, 0, 0, 1);
        }
      }
    }
  }

}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

public class LocalAdsbTcpProvider : MonoBehaviour{
    [SerializeField] private CesiumGeoreference georeference;
    [SerializeField] private GameObject aircraftPrefab;
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 30003;
    [SerializeField] private float aircraftScale = 300f;
    [SerializeField] private float removeAfterSeconds = 30f;

    private TcpClient client;
    private StreamReader reader;
    private Thread thread;
    private bool running;

    private readonly object lockObject = new();
    private readonly Queue<AdsbMessage> pendingMessages = new();
    private readonly Dictionary<string, AircraftRuntime> aircrafts = new();

    private void Start(){
        running = true;
        thread = new Thread(ReadLoop);
        thread.IsBackground = true;
        thread.Start();
    }

    private void Update(){
        ProcessMessages();
        RemoveOldAircrafts();
    }

    private void OnDestroy(){
        running = false;

        try{
            reader?.Close();
            client?.Close();
        }
        catch{
        }

        if (thread != null && thread.IsAlive)
            thread.Abort();
    }

    private void ReadLoop(){
        while (running){
            try{
                client = new TcpClient(host, port);
                reader = new StreamReader(client.GetStream());

                while (running && client.Connected){
                    string line = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    AdsbMessage message;

                    if (!TryParseSbsMessage(line, out message))
                        continue;

                    lock (lockObject){
                        pendingMessages.Enqueue(message);
                    }
                }
            }
            catch{
                Thread.Sleep(2000);
            }
        }
    }

    private void ProcessMessages(){
        while (true){
            AdsbMessage message;

            lock (lockObject){
                if (pendingMessages.Count == 0)
                    break;

                message = pendingMessages.Dequeue();
            }

            if (string.IsNullOrEmpty(message.icao24))
                continue;

            if (!message.hasPosition)
                continue;

            Vector3 unityPosition = ToUnityPosition(message.longitude, message.latitude, message.altitude);

            if (!aircrafts.TryGetValue(message.icao24, out AircraftRuntime runtime)){
                GameObject instance = Instantiate(aircraftPrefab, unityPosition, Quaternion.identity);
                instance.name = "Aircraft_" + message.icao24;
                instance.transform.localScale = Vector3.one * aircraftScale;

                runtime = new AircraftRuntime();
                runtime.transform = instance.transform;
                runtime.lastSeenTime = Time.time;

                aircrafts.Add(message.icao24, runtime);
            }

            runtime.targetPosition = unityPosition;
            runtime.lastSeenTime = Time.time;

            if (!string.IsNullOrEmpty(message.callsign))
                runtime.callsign = message.callsign;

            runtime.transform.position =
                Vector3.Lerp(runtime.transform.position, runtime.targetPosition, Time.deltaTime * 5f);

            if (message.hasTrack)
                runtime.transform.rotation = Quaternion.Euler(0f, message.track, 0f);
        }

        foreach (var pair in aircrafts){
            AircraftRuntime runtime = pair.Value;

            if (runtime.transform == null)
                continue;

            runtime.transform.position =
                Vector3.Lerp(runtime.transform.position, runtime.targetPosition, Time.deltaTime * 2f);
        }
    }

    private void RemoveOldAircrafts(){
        List<string> removeKeys = null;

        foreach (var pair in aircrafts){
            if (Time.time - pair.Value.lastSeenTime < removeAfterSeconds)
                continue;

            if (removeKeys == null)
                removeKeys = new List<string>();

            removeKeys.Add(pair.Key);
        }

        if (removeKeys == null)
            return;

        foreach (string key in removeKeys){
            if (aircrafts.TryGetValue(key, out AircraftRuntime runtime)){
                if (runtime.transform != null)
                    Destroy(runtime.transform.gameObject);

                aircrafts.Remove(key);
            }
        }
    }

    private bool TryParseSbsMessage(string line, out AdsbMessage message){
        message = new AdsbMessage();

        string[] p = line.Split(',');

        if (p.Length < 22)
            return false;

        if (p[0] != "MSG")
            return false;

        message.icao24 = p[4];
        message.callsign = p[10].Trim();

        if (TryDouble(p[11], out double altitude))
            message.altitude = altitude * 0.3048;
        else
            message.altitude = 10000;

        if (TryDouble(p[14], out double track)){
            message.track = (float)track;
            message.hasTrack = true;
        }

        bool hasLat = TryDouble(p[15], out double latitude);
        bool hasLon = TryDouble(p[16], out double longitude);

        if (hasLat && hasLon){
            message.latitude = latitude;
            message.longitude = longitude;
            message.hasPosition = true;
        }

        return true;
    }

    private bool TryDouble(string value, out double result){
        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private Vector3 ToUnityPosition(double longitude, double latitude, double height){
        double3 ecef =
            CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(longitude, latitude,
                height));
        double3 unity = georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);

        return new Vector3((float)unity.x, (float)unity.y, (float)unity.z);
    }
}

public class AircraftRuntime{
    public Transform transform;
    public Vector3 targetPosition;
    public float lastSeenTime;
    public string callsign;
}

public struct AdsbMessage{
    public string icao24;
    public string callsign;
    public double latitude;
    public double longitude;
    public double altitude;
    public float track;
    public bool hasPosition;
    public bool hasTrack;
}
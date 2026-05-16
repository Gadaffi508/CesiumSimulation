using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using CesiumForUnity;
using SimpleJSON;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;

public class OpenSkyAircraftManager : MonoBehaviour{
    [SerializeField] private CesiumGeoreference georeference;
    [SerializeField] private GameObject aircraftPrefab;

    [SerializeField] private float refreshRate = 10f;
    [SerializeField] private float altitudeOffset = 1000f;

    [Header("Turkey Bounds")] [SerializeField]
    private double minLatitude = 35.5;

    [SerializeField] private double maxLatitude = 42.5;

    [SerializeField] private double minLongitude = 25.5;
    [SerializeField] private double maxLongitude = 45.0;

    private readonly Dictionary<string, Transform> aircrafts = new();

    void Start(){
        StartCoroutine(UpdateLoop());
    }

    IEnumerator UpdateLoop(){
        while (true){
            yield return FetchAircrafts();

            yield return new WaitForSeconds(refreshRate);
        }
    }

    IEnumerator FetchAircrafts(){
        
        string url =
            "https://opensky-network.org/api/states/all" +
            $"?lamin={minLatitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&lomin={minLongitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&lamax={maxLatitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&lomax={maxLongitude.ToString(CultureInfo.InvariantCulture)}";

        Debug.Log(url);

        using UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success){
            Debug.LogError(request.error);
            yield break;
        }

        JSONNode json = JSON.Parse(request.downloadHandler.text);

        JSONArray states = json["states"].AsArray;

        if (states == null)
            yield break;

        foreach (JSONNode state in states){
            string id = state[0];

            double longitude = state[5].AsDouble;
            double latitude = state[6].AsDouble;
            double altitude = state[7].IsNull ? 10000.0 : state[7].AsDouble;
            altitude = Mathf.Clamp((float)altitude, 1000f, 12000f);

            if (longitude == 0 || latitude == 0)
                continue;

            Vector3 unityPosition =
                ToUnityPosition(longitude, latitude, altitude + altitudeOffset);

            if (!aircrafts.TryGetValue(id, out Transform aircraft)){
                GameObject instance =
                    Instantiate(aircraftPrefab, unityPosition, Quaternion.identity);

                instance.name = $"Aircraft_{id}";

                aircrafts.Add(id, instance.transform);

                aircraft = instance.transform;
            }

            aircraft.position =
                Vector3.Lerp(aircraft.position, unityPosition, 0.5f);

            float heading = state[10].AsFloat;

            aircraft.rotation =
                Quaternion.Euler(0f, heading, 0f);
        }
    }

    Vector3 ToUnityPosition(
        double longitude,
        double latitude,
        double height){
        double3 ecef =
            CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(
                new double3(longitude, latitude, height));

        double3 unity =
            georeference.TransformEarthCenteredEarthFixedPositionToUnity(
                ecef);

        return new Vector3(
            (float)unity.x,
            (float)unity.y,
            (float)unity.z);
    }
}
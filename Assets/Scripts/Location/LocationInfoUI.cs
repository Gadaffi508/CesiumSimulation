using System.Collections;
using System.Globalization;
using CesiumForUnity;
using SimpleJSON;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;

public class LocationInfoUI : MonoBehaviour{
    [SerializeField] private CesiumGeoreference georeference;
    [SerializeField] private Transform target;
    [SerializeField] private TMP_Text coordinateText;
    [SerializeField] private TMP_Text placeText;

    [SerializeField] private float placeUpdateInterval = 3f;
    [SerializeField] private double minCoordinateChangeForRequest = 0.01;

    private double lastRequestLatitude = 999;
    private double lastRequestLongitude = 999;
    private float timer;

    private void Start(){
        if (placeText != null)
            placeText.text = "Konum aranıyor...";
    }

    private void Update(){
        if (georeference == null || target == null)
            return;

        double3 unityPosition = new double3(
            target.position.x,
            target.position.y,
            target.position.z);

        double3 ecef =
            georeference.TransformUnityPositionToEarthCenteredEarthFixed(unityPosition);
        double3 lonLatHeight = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);

        double longitude = lonLatHeight.x;
        double latitude = lonLatHeight.y;
        double height = lonLatHeight.z;

        if (coordinateText != null){
            coordinateText.text =
                $"Lat: {latitude:F6}\n" +
                $"Lon: {longitude:F6}\n" +
                $"Height: {height:F0} m";
        }

        timer += Time.deltaTime;

        if (timer < placeUpdateInterval)
            return;

        timer = 0f;

        double diffLat = System.Math.Abs(latitude - lastRequestLatitude);
        double diffLon = System.Math.Abs(longitude - lastRequestLongitude);

        if (diffLat < minCoordinateChangeForRequest && diffLon < minCoordinateChangeForRequest)
            return;

        lastRequestLatitude = latitude;
        lastRequestLongitude = longitude;

        StartCoroutine(UpdatePlace(latitude, longitude));
    }

    private IEnumerator UpdatePlace(double latitude, double longitude){
        string lat = latitude.ToString(CultureInfo.InvariantCulture);
        string lon = longitude.ToString(CultureInfo.InvariantCulture);

        string url =
            $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={lat}&lon={lon}&zoom=10&addressdetails=1";

        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("User-Agent", "UnityEarthViewerPrototype/1.0");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success){
            if (placeText != null)
                placeText.text = "Konum bilgisi alınamadı";

            yield break;
        }

        JSONNode json = JSON.Parse(request.downloadHandler.text);
        JSONNode address = json["address"];

        string country = GetValue(address, "country");
        string city = GetValue(address, "city");

        if (string.IsNullOrEmpty(city))
            city = GetValue(address, "province");

        if (string.IsNullOrEmpty(city))
            city = GetValue(address, "state");

        if (string.IsNullOrEmpty(city))
            city = GetValue(address, "town");

        if (string.IsNullOrEmpty(city))
            city = GetValue(address, "county");

        if (string.IsNullOrEmpty(country))
            country = "Bilinmeyen ülke";

        if (string.IsNullOrEmpty(city))
            city = "Bilinmeyen şehir";

        if (placeText != null)
            placeText.text = $"{country}\n{city}";
    }

    private string GetValue(JSONNode node, string key){
        if (node == null)
            return "";

        if (node[key] == null)
            return "";

        return node[key].Value;
    }
}
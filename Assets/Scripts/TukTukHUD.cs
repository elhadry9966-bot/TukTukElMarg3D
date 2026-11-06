using UnityEngine;
using UnityEngine.UI;

public sealed class TukTukHUD : MonoBehaviour
{
    [SerializeField] private TukTukController vehicle;
    [SerializeField] private TukTukCamera vehicleCamera;
    [SerializeField] private Text speedText;
    [SerializeField] private Text cameraModeText;

    public void Configure(
        TukTukController controlledVehicle,
        TukTukCamera followCamera,
        Text speedLabel,
        Text cameraModeLabel)
    {
        vehicle = controlledVehicle;
        vehicleCamera = followCamera;
        speedText = speedLabel;
        cameraModeText = cameraModeLabel;
    }

    private void Update()
    {
        if (speedText != null)
            speedText.text = vehicle != null
                ? $"السرعة: {Mathf.RoundToInt(vehicle.CurrentSpeedKph)} كم/س"
                : "السرعة: 0 كم/س";

        if (cameraModeText != null && vehicleCamera != null)
            cameraModeText.text = vehicleCamera.IsFirstPerson
                ? "الكاميرا: منظور السائق (زر الكاميرا / V)"
                : "الكاميرا: خلفية (زر الكاميرا / V)";
    }
}

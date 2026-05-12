using UnityEngine;

public class EarthFreeCamera : MonoBehaviour{
    public float moveSpeed = 5000f;
    public float fastSpeed = 20000f;
    public float zoomSpeed = 50000f;
    public float rotationSpeed = 3f;

    float yaw;
    float pitch;

    void Start(){
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void Update(){
        if (Input.GetMouseButton(1)){
            yaw += Input.GetAxis("Mouse X") * rotationSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;

            pitch = Mathf.Clamp(pitch, -89f, 89f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        float speed = Input.GetKey(KeyCode.LeftShift)
            ? fastSpeed
            : moveSpeed;

        Vector3 move =
            transform.forward * Input.GetAxis("Vertical") +
            transform.right * Input.GetAxis("Horizontal");

        transform.position += move * speed * Time.deltaTime;

        float scroll = Input.mouseScrollDelta.y;

        transform.position += transform.forward * scroll * zoomSpeed * Time.deltaTime;
    }
}
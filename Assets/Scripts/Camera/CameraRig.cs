using UnityEngine;

namespace FlyingChick
{
    // Follows the bird and dynamically zooms out while it is high above the
    // terrain (e.g. after a big launch), then smoothly zooms back to normal
    // once it lands. This is the "jump above screen size -> auto zoom out and
    // back" requirement from the design.
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        [Header("Zoom")]
        [SerializeField] private float baseOrthoSize = 6f;
        [SerializeField] private float maxOrthoSize = 12f;
        [SerializeField] private float zoomStartHeight = 4f;
        [SerializeField] private float zoomPerHeightUnit = 0.5f;
        [SerializeField] private float zoomSmoothTime = 0.25f;

        [Header("Follow")]
        [SerializeField] private float lookAheadDistance = 3f;
        [SerializeField] private float followSmoothTime = 0.2f;

        private BirdController target;
        private Camera cam;
        private float zoomVelocity;
        private Vector3 followVelocity;

        public void SetTarget(BirdController newTarget) => target = newTarget;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = baseOrthoSize;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float extraHeight = Mathf.Max(0f, target.HeightAboveGround - zoomStartHeight);
            float desiredSize = Mathf.Clamp(baseOrthoSize + extraHeight * zoomPerHeightUnit, baseOrthoSize, maxOrthoSize);
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, desiredSize, ref zoomVelocity, zoomSmoothTime);

            Vector3 desiredPos = target.transform.position + new Vector3(lookAheadDistance, 0f, 0f);
            desiredPos.z = transform.position.z;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref followVelocity, followSmoothTime);
        }
    }
}

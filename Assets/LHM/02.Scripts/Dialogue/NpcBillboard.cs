using UnityEngine;

namespace RealSteel.Dialogue
{
    public class NpcBillboard : MonoBehaviour
    {
        [SerializeField] private bool uprightOnly = true;

        private static Camera[] buffer = new Camera[8];

        private void LateUpdate()
        {
            Camera camera = FindRenderingCamera();
            if (camera == null) return;

            Vector3 forward = camera.transform.forward;
            if (uprightOnly)
            {
                forward = Vector3.ProjectOnPlane(forward, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f) return;
            }

            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private static Camera FindRenderingCamera()
        {
            int count = Camera.allCamerasCount;
            if (count == 0) return null;
            if (buffer.Length < count) buffer = new Camera[count];
            Camera.GetAllCameras(buffer);

            Camera best = null;
            for (int i = 0; i < count; i++)
            {
                Camera camera = buffer[i];
                if (camera == null || camera.targetTexture != null) continue;
                if (best == null || camera.depth > best.depth) best = camera;
            }
            return best;
        }
    }
}

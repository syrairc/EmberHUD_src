using System.Numerics;
using Aember.Host.Memory;

namespace Aember.Host.Reading;

internal sealed class CameraReader(IReadBackend memory)
{
    public Camera? Read(nint pawn)
    {
        if (!memory.TryReadPtr(pawn + Offsets.PawnController, out var controller) ||
            !memory.TryReadPtr(controller + Offsets.ControllerCameraManager, out var manager) ||
            !memory.TryReadArray(manager + Offsets.CameraCache + Offsets.PovLocation, 3, out float[] location) ||
            !memory.TryReadArray(manager + Offsets.CameraCache + Offsets.PovRotation, 3, out float[] rotation) ||
            !memory.TryRead(manager + Offsets.CameraCache + Offsets.PovFov, out float fov) ||
            !float.IsFinite(fov) || fov is <= 0 or >= 180 || !location.Concat(rotation).All(float.IsFinite))
            return null;

        return new Camera(new Vector3(location[0], location[1], location[2]), new Vector3(rotation[0], rotation[1], rotation[2]), fov);
    }
}
